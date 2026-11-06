namespace WayniBcra.Domain;

/// <summary>
/// Lifecycle status of an outbox message (Transactional Outbox pattern).
/// Pending -> Processing (reserved by a dispatcher) -> Sent / Failed (terminal after retries).
/// </summary>
public enum OutboxStatus
{
    Pending = 0,
    Processing = 1,
    Sent = 2,
    Failed = 3
}
