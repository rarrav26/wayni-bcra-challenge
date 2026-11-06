using WayniBcra.Domain;

namespace WayniBcra.Application.Queries;

/// <summary>
/// Read-only access to debtor aggregates for the query endpoints. Exposes only the operations used
/// (lookup by identification number, top N by total financing). No generic CRUD.
/// </summary>
public interface IDebtorReadRepository
{
    Task<Debtor?> GetByIdentificationNumberAsync(string identificationNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Top <paramref name="count"/> debtors by total financing (highest first). When
    /// <paramref name="creditStatus"/> is provided, only debtors whose max credit status matches it
    /// are returned (optional filter by situación).
    /// </summary>
    Task<IReadOnlyList<Debtor>> GetTopByTotalFinancingAsync(
        int count, string? creditStatus = null, CancellationToken cancellationToken = default);
}
