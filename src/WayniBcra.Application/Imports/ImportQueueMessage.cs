namespace WayniBcra.Application.Imports;

/// <summary>
/// Message published to the queue when a file is uploaded, consumed by the Worker to process the import.
/// Carries the import identity and the storage key of the uploaded file.
/// </summary>
public sealed record ImportQueueMessage
{
    /// <summary>Identifies the import job (also used for idempotency).</summary>
    public required Guid ImportJobId { get; init; }

    /// <summary>Storage key (e.g. S3 object key) of the uploaded file.</summary>
    public required string ObjectKey { get; init; }
}
