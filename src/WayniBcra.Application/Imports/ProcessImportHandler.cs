using Microsoft.Extensions.Logging;

namespace WayniBcra.Application.Imports;

/// <summary>
/// Orchestrates the processing of an import: retrieves the job, transitions it through its lifecycle
/// (Start -> Complete/Fail), and delegates reading and aggregation to the reader and processor.
/// Invoked by the Worker when a queue message is consumed. No interface: the varying parts
/// (reading, processing) are abstracted behind their own interfaces (ADR-020).
/// </summary>
public sealed class ProcessImportHandler
{
    private readonly IImportJobRepository _importJobRepository;
    private readonly IImportRecordReader _recordReader;
    private readonly IImportProcessor _importProcessor;
    private readonly ILogger<ProcessImportHandler> _logger;

    public ProcessImportHandler(
        IImportJobRepository importJobRepository,
        IImportRecordReader recordReader,
        IImportProcessor importProcessor,
        ILogger<ProcessImportHandler> logger)
    {
        _importJobRepository = importJobRepository;
        _recordReader = recordReader;
        _importProcessor = importProcessor;
        _logger = logger;
    }

    /// <summary>
    /// Processes the import identified by <paramref name="importJobId"/> reading from
    /// <paramref name="fileStream"/>. On failure, marks the job as failed and rethrows so the
    /// queue can retry (idempotent reprocessing, ADR-016).
    /// </summary>
    public async Task HandleAsync(Guid importJobId, Stream fileStream, CancellationToken cancellationToken = default)
    {
        var importJob = await _importJobRepository.GetByIdAsync(importJobId, cancellationToken);
        if (importJob is null)
        {
            throw new InvalidOperationException($"Import job '{importJobId}' was not found.");
        }

        importJob.Start();
        await _importJobRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Started processing import {ImportJobId} ({FileName}).",
            importJob.Id, importJob.FileName);

        try
        {
            var records = _recordReader.ReadAsync(fileStream, cancellationToken);
            var result = await _importProcessor.ProcessAsync(importJob.Id, records, cancellationToken);

            importJob.Complete(result.AggregatedDebtors, result.AggregatedEntities);
            await _importJobRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Completed import {ImportJobId}: {Records} records, {Debtors} debtors, {Entities} entities, duration {Duration}.",
                importJob.Id, result.ProcessedRecords, result.AggregatedDebtors, result.AggregatedEntities, importJob.Duration);
        }
        catch (Exception ex)
        {
            importJob.Fail(ex.Message);
            await _importJobRepository.SaveChangesAsync(cancellationToken);

            _logger.LogError(ex, "Import {ImportJobId} failed.", importJob.Id);
            throw;
        }
    }
}
