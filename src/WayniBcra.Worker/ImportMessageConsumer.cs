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
        ImportQueueMessage? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ImportQueueMessage>(message.Body);
        }
        catch (Exception ex)
        {
            // Malformed message: cannot be processed. Delete it so it does not block the queue
            // (it would otherwise loop until the DLQ). Logged for diagnosis.
            _logger.LogError(ex, "Malformed queue message, deleting. Body length: {Length}.", message.Body?.Length ?? 0);
            await _sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, cancellationToken);
            return;
        }

        if (payload is null)
        {
            _logger.LogError("Null queue message payload, deleting.");
            await _sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, cancellationToken);
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
            var handler = scope.ServiceProvider.GetRequiredService<ProcessImportHandler>();

            await using var fileStream = await fileStorage.DownloadAsync(payload.ObjectKey, cancellationToken);
            await handler.HandleAsync(payload.ImportJobId, fileStream, cancellationToken);

            // Success: acknowledge by deleting the message.
            await _sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, cancellationToken);
            _logger.LogInformation("Processed and acknowledged import {ImportJobId}.", payload.ImportJobId);
        }
        catch (Exception ex)
        {
            // Do NOT delete: the message returns to the queue after the visibility timeout and is
            // retried (idempotent reprocessing). After maxReceiveCount it is moved to the DLQ.
            _logger.LogError(ex, "Failed to process import {ImportJobId}. Message will be retried.", payload.ImportJobId);
        }
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
