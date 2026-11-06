using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using WayniBcra.Application.Imports;
using WayniBcra.Infrastructure.Aws;

namespace WayniBcra.Worker;

/// <summary>
/// Background service that consumes import messages from SQS (long-polling), downloads the file from
/// storage and processes the import. On success the message is deleted (ack); on failure it is left
/// on the queue so SQS retries it, and after the configured max receives it goes to the DLQ.
/// </summary>
public sealed class ImportMessageConsumer : BackgroundService
{
    private readonly IAmazonSQS _sqs;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AwsOptions _awsOptions;
    private readonly ILogger<ImportMessageConsumer> _logger;

    public ImportMessageConsumer(
        IAmazonSQS sqs,
        IServiceScopeFactory scopeFactory,
        IOptions<AwsOptions> awsOptions,
        ILogger<ImportMessageConsumer> logger)
    {
        _sqs = sqs;
        _scopeFactory = scopeFactory;
        _awsOptions = awsOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueUrl = await ResolveQueueUrlWithRetryAsync(stoppingToken);
        _logger.LogInformation("Import consumer started. Listening on queue {QueueUrl}.", queueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = 1,
                    WaitTimeSeconds = 20 // long polling
                }, stoppingToken);

                // AWS SDK v4 may return a null Messages collection when the queue is empty (unlike v3's empty list).
                if (response.Messages is null || response.Messages.Count == 0)
                {
                    continue;
                }

                foreach (var message in response.Messages)
                {
                    await HandleMessageAsync(queueUrl, message, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
                break;
            }
            catch (Exception ex)
            {
                // Safety net: never let an unexpected exception tear down the consumer loop.
                // Log and keep polling (resilient to transient failures and unforeseen errors).
                _logger.LogError(ex, "Unexpected error in the consumer loop. Continuing after a short delay.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task HandleMessageAsync(string queueUrl, Message message, CancellationToken cancellationToken)
    {
        // When an S3 -> SQS notification is configured, S3 first sends a one-off "s3:TestEvent" to
        // validate the wiring. It carries no object, so skip it quietly (it is expected, not an error).
        if (message.Body is not null && message.Body.Contains("\"s3:TestEvent\"", StringComparison.Ordinal))
        {
            _logger.LogInformation("Ignoring S3 test event (notification setup), deleting.");
            await _sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, cancellationToken);
            return;
        }

        // The queue receives S3 "object created" events (S3 -> SQS). Extract the object key and
        // recover the import job id encoded in it.
        string? objectKey;
        try
        {
            objectKey = ExtractObjectKey(message.Body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Malformed S3 event, deleting. Body length: {Length}.", message.Body?.Length ?? 0);
            await _sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, cancellationToken);
            return;
        }

        if (objectKey is null || !ImportObjectKey.TryParseImportJobId(objectKey, out var importJobId))
        {
            _logger.LogWarning("S3 event without a recognizable import key ({ObjectKey}), deleting.", objectKey);
            await _sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, cancellationToken);
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
            var handler = scope.ServiceProvider.GetRequiredService<ProcessImportHandler>();

            // Pass a stream factory (not an open stream) so the handler can retry the download from
            // the start on a transient connection reset while reading a large object.
            await handler.HandleAsync(
                importJobId,
                ct => fileStorage.DownloadAsync(objectKey, ct),
                cancellationToken);

            // Success: acknowledge by deleting the message.
            await _sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, cancellationToken);
            _logger.LogInformation("Processed and acknowledged import {ImportJobId}.", importJobId);
        }
        catch (Exception ex)
        {
            // Do NOT delete: the message returns to the queue after the visibility timeout and is
            // retried (idempotent reprocessing). After maxReceiveCount it is moved to the DLQ.
            _logger.LogError(ex, "Failed to process import {ImportJobId}. Message will be retried.", importJobId);
        }
    }

    // Parses the S3 event and returns the (URL-decoded) object key, or null if the event has none.
    private static string? ExtractObjectKey(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var notification = JsonSerializer.Deserialize<S3EventNotification>(body);
        var rawKey = notification?.Records?.FirstOrDefault()?.S3?.Object?.Key;
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return null;
        }

        // S3 encodes the key in events (spaces as '+', etc.); decode it back to the real key.
        return Uri.UnescapeDataString(rawKey.Replace("+", " "));
    }

    private async Task<string> ResolveQueueUrlWithRetryAsync(CancellationToken cancellationToken)
    {
        // The queue may not be ready immediately at startup (LocalStack init). Retry until available.
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var response = await _sqs.GetQueueUrlAsync(_awsOptions.QueueName, cancellationToken);
                return response.QueueUrl;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Queue {QueueName} not available yet. Retrying in 5s.", _awsOptions.QueueName);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        throw new OperationCanceledException(cancellationToken);
    }
}
