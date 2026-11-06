using System.Text.Json;

namespace WayniBcra.Application.Notifications;

/// <summary>
/// Self-contained notification data for a completed import, serialized as JSON into the outbox
/// message payload. Carrying the summary here keeps the outbox message independent of other tables
/// when the dispatcher sends it (channel-agnostic, survives deferred delivery).
/// </summary>
public sealed record ImportCompletedPayload(
    Guid ImportJobId,
    string FileName,
    long Records,
    int Debtors,
    int Entities,
    double? DurationSeconds,
    DateTimeOffset CompletedAt)
{
    public string ToJson() => JsonSerializer.Serialize(this);

    public static ImportCompletedPayload FromJson(string json) =>
        JsonSerializer.Deserialize<ImportCompletedPayload>(json)
            ?? throw new InvalidOperationException("Invalid outbox payload.");
}
