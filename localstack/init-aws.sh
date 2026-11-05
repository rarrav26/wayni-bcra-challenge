#!/bin/bash
# LocalStack init script. Runs when LocalStack is ready (mounted into /etc/localstack/init/ready.d/).
# Provisions the S3 bucket and the SQS queues (main + DLQ) used by the system (ADR-026).
set -euo pipefail

BUCKET_NAME="${AWS_BUCKET_NAME:-wayni-bcra-imports}"
QUEUE_NAME="${AWS_QUEUE_NAME:-wayni-bcra-import-queue}"
DLQ_NAME="${AWS_DLQ_NAME:-wayni-bcra-import-dlq}"
MAX_RECEIVE_COUNT="${AWS_MAX_RECEIVE_COUNT:-3}"
VISIBILITY_TIMEOUT="${AWS_VISIBILITY_TIMEOUT:-900}"

echo "Creating S3 bucket: ${BUCKET_NAME}"
awslocal s3 mb "s3://${BUCKET_NAME}"

echo "Creating DLQ: ${DLQ_NAME}"
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

echo "LocalStack init complete."
