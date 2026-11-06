using System.Text.Json.Serialization;

namespace WayniBcra.Worker;

/// <summary>
/// Minimal shape of the S3 "object created" event delivered to SQS. Only the object key is needed;
/// the import job id is encoded in that key (see ImportObjectKey). The full AWS event has many more
/// fields, intentionally ignored here.
/// </summary>
public sealed class S3EventNotification
{
    [JsonPropertyName("Records")]
    public List<S3EventRecord>? Records { get; set; }
}

public sealed class S3EventRecord
{
    [JsonPropertyName("s3")]
    public S3Entity? S3 { get; set; }
}

public sealed class S3Entity
{
    [JsonPropertyName("object")]
    public S3Object? Object { get; set; }
}

public sealed class S3Object
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }
}
