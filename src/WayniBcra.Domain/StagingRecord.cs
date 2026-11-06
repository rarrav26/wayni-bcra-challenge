namespace WayniBcra.Domain;

/// <summary>
/// A raw parsed record belonging to a specific import, stored transiently in the staging table
/// before aggregation. Holds only the fields used by this solution, plus the owning import id to
/// isolate concurrent imports. Cleared per import after the aggregates are computed.
/// </summary>
public sealed class StagingRecord
{
    public long Id { get; private set; }

    /// <summary>Owning import, used to isolate records of concurrent imports.</summary>
    public Guid ImportJobId { get; private set; }

    /// <summary>Entity code (field 1).</summary>
    public string EntityCode { get; private set; }

    /// <summary>Identification type (field 3).</summary>
    public string IdentificationType { get; private set; }

    /// <summary>Identification number / CUIT-CUIL-CDI (field 4).</summary>
    public string IdentificationNumber { get; private set; }

    /// <summary>Credit status (field 6), 2-digit code.</summary>
    public string CreditStatus { get; private set; }

    /// <summary>Financing amount (field 7), in thousands of pesos with one decimal.</summary>
    public decimal FinancingAmount { get; private set; }

    private StagingRecord()
    {
        EntityCode = string.Empty;
        IdentificationType = string.Empty;
        IdentificationNumber = string.Empty;
        CreditStatus = string.Empty;
    }

    public StagingRecord(
        Guid importJobId,
        string entityCode,
        string identificationType,
        string identificationNumber,
        string creditStatus,
        decimal financingAmount)
    {
        ImportJobId = importJobId;
        EntityCode = entityCode;
        IdentificationType = identificationType;
        IdentificationNumber = identificationNumber;
        CreditStatus = creditStatus;
        FinancingAmount = financingAmount;
    }
}
