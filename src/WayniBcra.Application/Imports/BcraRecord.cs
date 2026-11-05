namespace WayniBcra.Application.Imports;

/// <summary>
/// Intermediate model representing a single parsed record from the BCRA fixed-width file,
/// holding only the fields needed by this solution. Decouples the domain and the aggregation
/// stage from the physical layout of the TXT file.
/// </summary>
/// <remarks>
/// Fixed-width fields are kept as strings, preserving their original format (padding, leading zeros),
/// per the unified string criterion (ADR-013). The only numeric field is the financing amount,
/// which is parsed to <see cref="decimal"/> because it is summed during aggregation.
/// </remarks>
public sealed record BcraRecord
{
    /// <summary>Entity code (field 1). 5 chars, e.g. "00007".</summary>
    public required string EntityCode { get; init; }

    /// <summary>Identification type (field 3). 2 chars, e.g. "11".</summary>
    public required string IdentificationType { get; init; }

    /// <summary>Identification number / CUIT-CUIL-CDI (field 4). 11 chars.</summary>
    public required string IdentificationNumber { get; init; }

    /// <summary>Credit status (field 6). 2 chars, e.g. "01", "05".</summary>
    public required string CreditStatus { get; init; }

    /// <summary>
    /// Financing amount (field 7), in thousands of pesos with one decimal.
    /// Parsed to decimal from the BCRA numeric format.
    /// </summary>
    public required decimal FinancingAmount { get; init; }
}
