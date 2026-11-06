namespace WayniBcra.Worker;

/// <summary>
/// Configuration for the <see cref="NotificationDispatcher"/> (bound from the "NotificationDispatcher"
/// section; defaults are sensible for local/demo).
/// </summary>
public sealed class NotificationDispatcherOptions
{
    public const string SectionName = "NotificationDispatcher";

    /// <summary>How often to poll the outbox when there is no pending work.</summary>
    public int PollIntervalSeconds { get; set; } = 10;

    /// <summary>Max messages claimed per poll.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>Max delivery attempts before a message is marked Failed (terminal).</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Base delay for the deferred (outbox) exponential backoff between attempts.</summary>
    public int RetryBaseDelaySeconds { get; set; } = 30;

    /// <summary>In-process (Polly) retry attempts for a transient send failure within one attempt.</summary>
    public int InProcessRetryAttempts { get; set; } = 2;
}
