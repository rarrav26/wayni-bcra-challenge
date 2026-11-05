using WayniBcra.Domain;

namespace WayniBcra.Application.Queries;

/// <summary>
/// Read-only access to financial entity aggregates for the query endpoints.
/// </summary>
public interface IFinancialEntityReadRepository
{
    Task<FinancialEntity?> GetByEntityCodeAsync(string entityCode, CancellationToken cancellationToken = default);
}
