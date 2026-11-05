using Microsoft.EntityFrameworkCore;
using WayniBcra.Application.Queries;
using WayniBcra.Domain;

namespace WayniBcra.Infrastructure.Persistence.Repositories;

public sealed class FinancialEntityReadRepository : IFinancialEntityReadRepository
{
    private readonly WayniBcraDbContext _dbContext;

    public FinancialEntityReadRepository(WayniBcraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<FinancialEntity?> GetByEntityCodeAsync(string entityCode, CancellationToken cancellationToken = default)
    {
        return _dbContext.FinancialEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EntityCode == entityCode, cancellationToken);
    }
}
