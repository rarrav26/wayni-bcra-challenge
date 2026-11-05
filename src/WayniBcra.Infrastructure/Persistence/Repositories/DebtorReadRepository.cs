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

    public async Task<IReadOnlyList<Debtor>> GetTopByTotalFinancingAsync(int count, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Debtors
            .AsNoTracking()
            .OrderByDescending(d => d.TotalFinancingAmount)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
