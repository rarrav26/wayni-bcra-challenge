namespace WayniBcra.Domain;

/// <summary>
/// Aggregated result for a single financial entity, identified by its entity code (field 1).
/// Holds the total financing amount (field 7) summed across the entity's records.
/// </summary>
public sealed class FinancialEntity
{
    /// <summary>
    /// Entity code (field 1).
    /// </summary>
    public string EntityCode { get; private set; }

    /// <summary>
    /// Total financing amount (field 7) summed across the entity's records.
    /// Expressed in thousands of pesos with one decimal.
    /// </summary>
    public decimal TotalFinancingAmount { get; private set; }

    private FinancialEntity()
    {
        EntityCode = string.Empty;
    }

    public FinancialEntity(string entityCode, decimal totalFinancingAmount)
    {
        if (string.IsNullOrWhiteSpace(entityCode))
        {
            throw new ArgumentException("Entity code is required.", nameof(entityCode));
        }

        EntityCode = entityCode;
        TotalFinancingAmount = totalFinancingAmount;
    }
}
