using WayniBcra.Domain;

namespace WayniBcra.Application.Notifications;

/// <summary>
/// Persistence for <see cref="OutboxMessage"/> (Transactional Outbox). Exposes only what the
/// import flow and the notification dispatcher need (no generic CRUD, per ADR-020).
/// </summary>
public interface IOutboxRepository
{
    /// <summary>
    /// Stages a new outbox message. It is persisted by the caller's SaveChanges, so it can be
    /// committed in the same transaction that completes the import (atomic intent to notify).
    /// </summary>
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims up to <paramref name="batchSize"/> dispatchable messages (status Pending,
    /// next_attempt_at due), marking each as Processing, and returns them. Uses row locking
    /// (FOR UPDATE SKIP LOCKED) so multiple dispatchers can run without picking the same rows.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the state changes made to a claimed message (Sent / Failed / back to Pending).
    /// </summary>
    Task UpdateAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
