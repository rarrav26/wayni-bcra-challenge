namespace WayniBcra.Application.Imports;

/// <summary>
/// Summary of a completed import: how many records were processed, how many debtors and entities
/// resulted from the aggregation. Feeds the ImportJob summary.
/// </summary>
public sealed record ImportResult
{
    public required long ProcessedRecords { get; init; }
    public required int AggregatedDebtors { get; init; }
    public required int AggregatedEntities { get; init; }
}
