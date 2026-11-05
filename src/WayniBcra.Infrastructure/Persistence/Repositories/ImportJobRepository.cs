using Microsoft.EntityFrameworkCore;
using WayniBcra.Application.Imports;
using WayniBcra.Domain;

namespace WayniBcra.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IImportJobRepository"/>.
/// </summary>
public sealed class ImportJobRepository : IImportJobRepository
{
    private readonly WayniBcraDbContext _dbContext;

    public ImportJobRepository(WayniBcraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(ImportJob importJob, CancellationToken cancellationToken = default)
    {
        await _dbContext.ImportJobs.AddAsync(importJob, cancellationToken);
    }

    public Task<ImportJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.ImportJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
