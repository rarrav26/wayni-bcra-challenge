using System.Text.Json.Serialization;
using WayniBcra.Domain;

namespace WayniBcra.Api.Contracts;

/// <summary>
/// API response for a financial entity. JSON field names follow the challenge statement (Spanish).
/// </summary>
public sealed record FinancialEntityResponse
{
    [JsonPropertyName("codigo_entidad")]
    public required string EntityCode { get; init; }

    [JsonPropertyName("suma_total_prestamos")]
    public required decimal TotalFinancingAmount { get; init; }

    public static FinancialEntityResponse FromDomain(FinancialEntity entity) => new()
    {
        EntityCode = entity.EntityCode,
        TotalFinancingAmount = entity.TotalFinancingAmount
    };
}
