using WayniBcra.Domain;

namespace WayniBcra.Application.Imports;

/// <summary>
/// Persistence for <see cref="ImportJob"/>. Exposes only the operations used by the import flow
/// (no generic CRUD).
/// </summary>
public interface IImportJobRepository
{
    Task AddAsync(ImportJob importJob, CancellationToken cancellationToken = default);

    Task<ImportJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
