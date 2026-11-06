namespace WayniBcra.Domain;

/// <summary>
/// Aggregated result for a single debtor, identified by their identification number (field 4).
/// Values are computed during import (MAX credit status, SUM of financing amount) and stored here;
/// query endpoints read these aggregates directly.
/// </summary>
public sealed class Debtor
{
    /// <summary>
    /// Identification number (CUIT/CUIL/CDI, field 4). Kept as string to preserve leading zeros.
    /// </summary>
    public string IdentificationNumber { get; private set; }

    /// <summary>
    /// Credit status of highest credit severity (field 6) across the debtor's records,
    /// selected via the severity ranking. The original 2-digit BCRA code is stored
    /// (e.g. "01", "05", "11"), never the ranking value.
    /// </summary>
    public string MaxCreditStatus { get; private set; }

    /// <summary>
    /// Total financing amount (field 7) summed across the debtor's records.
    /// Expressed in thousands of pesos with one decimal.
    /// </summary>
    public decimal TotalFinancingAmount { get; private set; }

    private Debtor()
    {
        IdentificationNumber = string.Empty;
        MaxCreditStatus = string.Empty;
    }

    public Debtor(string identificationNumber, string maxCreditStatus, decimal totalFinancingAmount)
    {
        if (string.IsNullOrWhiteSpace(identificationNumber))
        {
            throw new ArgumentException("Identification number is required.", nameof(identificationNumber));
        }

        if (string.IsNullOrWhiteSpace(maxCreditStatus))
        {
            throw new ArgumentException("Max credit status is required.", nameof(maxCreditStatus));
        }

        IdentificationNumber = identificationNumber;
        MaxCreditStatus = maxCreditStatus;
        TotalFinancingAmount = totalFinancingAmount;
    }
}
