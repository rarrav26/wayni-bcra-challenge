namespace WayniBcra.Application.Imports;

/// <summary>
/// Publishes import messages to the queue (implemented over SQS). The application does not depend
/// on the AWS SDK directly.
/// </summary>
public interface IImportQueuePublisher
{
    Task PublishAsync(ImportQueueMessage message, CancellationToken cancellationToken = default);
}
