using System.Text.Json.Serialization;
using WayniBcra.Domain;

namespace WayniBcra.Api.Contracts;

/// <summary>
/// API response for a debtor. JSON field names follow the challenge statement (Spanish), mapped via
/// JsonPropertyName. The domain stays in English; translation is confined to this presentation DTO.
/// </summary>
public sealed record DebtorResponse
{
    [JsonPropertyName("nro_identificacion")]
    public required string IdentificationNumber { get; init; }

    [JsonPropertyName("situacion_maxima")]
    public required string MaxCreditStatus { get; init; }

    [JsonPropertyName("suma_total_prestamos")]
    public required decimal TotalFinancingAmount { get; init; }

    public static DebtorResponse FromDomain(Debtor debtor) => new()
    {
        IdentificationNumber = debtor.IdentificationNumber,
        MaxCreditStatus = debtor.MaxCreditStatus,
        TotalFinancingAmount = debtor.TotalFinancingAmount
    };
}
