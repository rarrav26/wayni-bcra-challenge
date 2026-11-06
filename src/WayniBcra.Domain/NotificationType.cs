namespace WayniBcra.Domain;

/// <summary>
/// Channel used to deliver an outbox notification. Only Email is implemented; the outbox is
/// channel-agnostic by design, so adding Webhook would be a new value plus a sender adapter.
/// </summary>
public enum NotificationType
{
    Email = 0
}
