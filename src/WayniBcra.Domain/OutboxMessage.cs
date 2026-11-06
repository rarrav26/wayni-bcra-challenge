namespace WayniBcra.Domain;

/// <summary>
/// A pending notification persisted in the same transaction that completes an import
/// (Transactional Outbox pattern). The message is self-contained: its <see cref="Payload"/> carries
/// the notification data (JSON) so the dispatcher does not depend on other tables when sending.
/// Encapsulates the valid state transitions so they cannot be expressed incorrectly from outside.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public Guid ImportJobId { get; private set; }
    public NotificationType NotificationType { get; private set; }

    /// <summary>Self-contained notification data as JSON (channel-agnostic).</summary>
    public string Payload { get; private set; }

    public OutboxStatus Status { get; private set; }

    /// <summary>Number of delivery attempts made so far.</summary>
    public int Attempts { get; private set; }

    /// <summary>Earliest time at which the message may be dispatched (drives retry backoff).</summary>
    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>Last delivery error, populated when an attempt fails.</summary>
    public string? LastError { get; private set; }

    private OutboxMessage(
        Guid id,
        Guid importJobId,
        NotificationType notificationType,
        string payload,
        DateTimeOffset createdAt)
    {
        Id = id;
        ImportJobId = importJobId;
        NotificationType = notificationType;
        Payload = payload;
        Status = OutboxStatus.Pending;
        NextAttemptAt = createdAt;
        CreatedAt = createdAt;
    }

    // Parameterless constructor for EF Core materialization.
    private OutboxMessage()
    {
        Payload = string.Empty;
    }

    /// <summary>
    /// Creates a new outbox message in the Pending state, available for immediate dispatch.
    /// </summary>
    public static OutboxMessage Create(
        Guid importJobId,
        NotificationType notificationType,
        string payload,
        TimeProvider? timeProvider = null)
    {
        if (importJobId == Guid.Empty)
        {
            throw new ArgumentException("Import job id is required.", nameof(importJobId));
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Payload is required.", nameof(payload));
        }

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        return new OutboxMessage(Guid.NewGuid(), importJobId, notificationType, payload, now);
    }

    /// <summary>
    /// Reserves the message for dispatch (Pending -> Processing). The dispatcher calls this after
    /// selecting the row with locking, so a concurrent dispatcher does not pick it up again.
    /// </summary>
    public void Reserve()
    {
        EnsureStatus(OutboxStatus.Pending, nameof(Reserve));
        Status = OutboxStatus.Processing;
        Attempts++;
    }

    /// <summary>
    /// Marks the message as successfully sent (Processing -> Sent).
    /// </summary>
    public void MarkSent(TimeProvider? timeProvider = null)
    {
        EnsureStatus(OutboxStatus.Processing, nameof(MarkSent));
        Status = OutboxStatus.Sent;
        SentAt = (timeProvider ?? TimeProvider.System).GetUtcNow();
        LastError = null;
    }

    /// <summary>
    /// Records a failed delivery attempt (from Processing). If attempts remain, the message goes
    /// back to Pending with a future <see cref="NextAttemptAt"/> (exponential backoff) so it is
    /// retried later. Once <paramref name="maxAttempts"/> is reached it becomes Failed (terminal).
    /// </summary>
    public void MarkFailed(string error, int maxAttempts, TimeSpan baseRetryDelay, TimeProvider? timeProvider = null)
    {
        EnsureStatus(OutboxStatus.Processing, nameof(MarkFailed));

        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        LastError = string.IsNullOrWhiteSpace(error) ? "Unknown error." : error;

        if (Attempts >= maxAttempts)
        {
            Status = OutboxStatus.Failed;
            return;
        }

        // Exponential backoff: baseDelay * 2^(attempts-1), e.g. 30s, 60s, 120s...
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        var backoffFactor = Math.Pow(2, Attempts - 1);
        NextAttemptAt = now + baseRetryDelay * backoffFactor;
        Status = OutboxStatus.Pending;
    }

    private void EnsureStatus(OutboxStatus expected, string operation)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException(
                $"Cannot {operation} an outbox message in status '{Status}'. Expected '{expected}'.");
        }
    }
}
