using Microsoft.EntityFrameworkCore;
using WayniBcra.Application.Notifications;
using WayniBcra.Domain;

namespace WayniBcra.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IOutboxRepository"/>.
/// - AddAsync shares the scoped DbContext with the import flow, so a staged message is committed by
///   the same SaveChanges that completes the import (atomic intent to notify).
/// - ClaimPendingAsync reserves due messages using row locking (FOR UPDATE SKIP LOCKED) so multiple
///   dispatchers can run concurrently without delivering the same message twice.
/// </summary>
public sealed class OutboxRepository : IOutboxRepository
{
    private readonly WayniBcraDbContext _dbContext;

    public OutboxRepository(WayniBcraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await _dbContext.OutboxMessages.AddAsync(message, cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
        int batchSize, CancellationToken cancellationToken = default)
    {
        // Select and lock due Pending rows, skipping rows locked by other dispatchers. The lock is
        // held until the surrounding transaction commits, which is why we mark them Processing and
        // commit here: once committed, no other dispatcher can see them as Pending.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var claimed = await _dbContext.OutboxMessages
            .FromSqlInterpolated($@"
SELECT * FROM outbox_messages
WHERE status = {(int)OutboxStatus.Pending} AND next_attempt_at <= NOW()
ORDER BY next_attempt_at
LIMIT {batchSize}
FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);

        foreach (var message in claimed)
        {
            message.Reserve(); // Pending -> Processing, increments attempts.
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return claimed;
    }

    public async Task UpdateAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        _dbContext.OutboxMessages.Update(message);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
