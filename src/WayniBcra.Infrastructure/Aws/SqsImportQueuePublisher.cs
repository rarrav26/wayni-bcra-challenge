using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using WayniBcra.Application.Imports;

namespace WayniBcra.Infrastructure.Aws;

/// <summary>
/// SQS-backed publisher for import messages. The queue is assumed to exist (provisioned by the
/// LocalStack init script). Serializes the message as JSON.
/// </summary>
public sealed class SqsImportQueuePublisher : IImportQueuePublisher
{
    private readonly IAmazonSQS _sqs;
    private readonly AwsOptions _options;

    public SqsImportQueuePublisher(IAmazonSQS sqs, IOptions<AwsOptions> options)
    {
        _sqs = sqs;
        _options = options.Value;
    }

    public async Task PublishAsync(ImportQueueMessage message, CancellationToken cancellationToken = default)
    {
        var queueUrl = await ResolveQueueUrlAsync(cancellationToken);
        var body = JsonSerializer.Serialize(message);

        await _sqs.SendMessageAsync(
            new SendMessageRequest { QueueUrl = queueUrl, MessageBody = body },
            cancellationToken);
    }

    private async Task<string> ResolveQueueUrlAsync(CancellationToken cancellationToken)
    {
        var response = await _sqs.GetQueueUrlAsync(_options.QueueName, cancellationToken);
        return response.QueueUrl;
    }
}
