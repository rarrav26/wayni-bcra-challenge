#!/bin/bash
# LocalStack init script. Runs when LocalStack is ready (mounted into /etc/localstack/init/ready.d/).
# Provisions the S3 bucket and the SQS queues (main + DLQ) used by the system (ADR-026).
# Idempotent: with PERSISTENCE enabled the state survives restarts, so the script must run safely
# against already-provisioned resources (create-queue is naturally idempotent; the bucket is guarded).
set -euo pipefail

BUCKET_NAME="${AWS_BUCKET_NAME:-wayni-bcra-imports}"
QUEUE_NAME="${AWS_QUEUE_NAME:-wayni-bcra-import-queue}"
DLQ_NAME="${AWS_DLQ_NAME:-wayni-bcra-import-dlq}"
MAX_RECEIVE_COUNT="${AWS_MAX_RECEIVE_COUNT:-3}"
VISIBILITY_TIMEOUT="${AWS_VISIBILITY_TIMEOUT:-900}"

echo "Ensuring S3 bucket: ${BUCKET_NAME}"
if ! awslocal s3api head-bucket --bucket "${BUCKET_NAME}" 2>/dev/null; then
  awslocal s3 mb "s3://${BUCKET_NAME}"
else
  echo "Bucket ${BUCKET_NAME} already exists, skipping creation."
fi

echo "Ensuring DLQ: ${DLQ_NAME}"
awslocal sqs create-queue --queue-name "${DLQ_NAME}"

# Resolve the DLQ ARN to wire the redrive policy.
DLQ_URL=$(awslocal sqs get-queue-url --queue-name "${DLQ_NAME}" --query 'QueueUrl' --output text)
DLQ_ARN=$(awslocal sqs get-queue-attributes --queue-url "${DLQ_URL}" --attribute-names QueueArn --query 'Attributes.QueueArn' --output text)

echo "Creating main queue: ${QUEUE_NAME} (visibility ${VISIBILITY_TIMEOUT}s, maxReceiveCount ${MAX_RECEIVE_COUNT})"
awslocal sqs create-queue \
  --queue-name "${QUEUE_NAME}" \
  --attributes "{
    \"VisibilityTimeout\": \"${VISIBILITY_TIMEOUT}\",
    \"RedrivePolicy\": \"{\\\"deadLetterTargetArn\\\":\\\"${DLQ_ARN}\\\",\\\"maxReceiveCount\\\":\\\"${MAX_RECEIVE_COUNT}\\\"}\"
  }"

QUEUE_URL=$(awslocal sqs get-queue-url --queue-name "${QUEUE_NAME}" --query 'QueueUrl' --output text)
QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url "${QUEUE_URL}" --attribute-names QueueArn --query 'Attributes.QueueArn' --output text)

# Allow S3 to send messages to the queue (required for the S3 -> SQS event notification).
echo "Granting S3 permission to publish to the queue"
awslocal sqs set-queue-attributes \
  --queue-url "${QUEUE_URL}" \
  --attributes "{
    \"Policy\": \"{\\\"Version\\\":\\\"2012-10-17\\\",\\\"Statement\\\":[{\\\"Effect\\\":\\\"Allow\\\",\\\"Principal\\\":{\\\"Service\\\":\\\"s3.amazonaws.com\\\"},\\\"Action\\\":\\\"sqs:SendMessage\\\",\\\"Resource\\\":\\\"${QUEUE_ARN}\\\"}]}\"
  }"

# Configure the bucket to publish an event to the queue whenever an object is created.
# This triggers processing automatically after the client's direct upload (no explicit publish).
echo "Configuring S3 -> SQS event notification on ${BUCKET_NAME}"
awslocal s3api put-bucket-notification-configuration \
  --bucket "${BUCKET_NAME}" \
  --notification-configuration "{
    \"QueueConfigurations\": [
      {
        \"QueueArn\": \"${QUEUE_ARN}\",
        \"Events\": [\"s3:ObjectCreated:*\"]
      }
    ]
  }"

# CORS on the bucket so the browser can PUT directly to S3 from the frontend origin.
echo "Configuring CORS on ${BUCKET_NAME}"
awslocal s3api put-bucket-cors \
  --bucket "${BUCKET_NAME}" \
  --cors-configuration "{
    \"CORSRules\": [
      {
        \"AllowedOrigins\": [\"*\"],
        \"AllowedMethods\": [\"PUT\", \"GET\"],
        \"AllowedHeaders\": [\"*\"]
      }
    ]
  }"

echo "LocalStack init complete."
