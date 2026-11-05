using WayniBcra.Domain;

namespace WayniBcra.Application.Queries;

/// <summary>
/// Read-only access to debtor aggregates for the query endpoints. Exposes only the operations used
/// (lookup by identification number, top N by total financing). No generic CRUD (ADR-020).
/// </summary>
public interface IDebtorReadRepository
{
    Task<Debtor?> GetByIdentificationNumberAsync(string identificationNumber, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Debtor>> GetTopByTotalFinancingAsync(int count, CancellationToken cancellationToken = default);
}
