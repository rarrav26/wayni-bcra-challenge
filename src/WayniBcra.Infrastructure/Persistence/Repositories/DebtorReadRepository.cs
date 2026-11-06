using Microsoft.EntityFrameworkCore;
using WayniBcra.Application.Queries;
using WayniBcra.Domain;

namespace WayniBcra.Infrastructure.Persistence.Repositories;

public sealed class DebtorReadRepository : IDebtorReadRepository
{
    private readonly WayniBcraDbContext _dbContext;

    public DebtorReadRepository(WayniBcraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Debtor?> GetByIdentificationNumberAsync(string identificationNumber, CancellationToken cancellationToken = default)
    {
        return _dbContext.Debtors
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.IdentificationNumber == identificationNumber, cancellationToken);
    }

    public async Task<IReadOnlyList<Debtor>> GetTopByTotalFinancingAsync(
        int count, string? creditStatus = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Debtors.AsNoTracking();

        // Optional filter by situación: when a status is given, restrict to debtors whose max
        // credit status matches it. Applied before ordering/paging so the top N is within the filter.
        if (!string.IsNullOrWhiteSpace(creditStatus))
        {
            query = query.Where(d => d.MaxCreditStatus == creditStatus);
        }

        return await query
            .OrderByDescending(d => d.TotalFinancingAmount)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
