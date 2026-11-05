namespace WayniBcra.Application.Imports;

/// <summary>
/// Ingests BCRA records for a given import into staging (COPY) and computes the aggregations
/// (debtors and financial entities), persisting the results transactionally. Implemented in
/// Infrastructure; the use case in Application orchestrates the ImportJob lifecycle around it.
/// </summary>
public interface IImportProcessor
{
    /// <summary>
    /// Loads the records of <paramref name="importJobId"/> into staging and aggregates them into
    /// the final tables, in a single transaction (idempotent via upsert).
    /// </summary>
    /// <param name="importJobId">Identifies the import; isolates its staging rows.</param>
    /// <param name="records">The parsed records to ingest (streamed).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A summary of the processing.</returns>
    Task<ImportResult> ProcessAsync(
        Guid importJobId,
        IAsyncEnumerable<BcraRecord> records,
        CancellationToken cancellationToken = default);
}
