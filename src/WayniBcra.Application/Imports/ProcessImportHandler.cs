using Microsoft.Extensions.Logging;
using WayniBcra.Application.Notifications;
using WayniBcra.Domain;

namespace WayniBcra.Application.Imports;

/// <summary>
/// Orchestrates the processing of an import: retrieves the job, transitions it through its lifecycle
/// (Start -> Complete/Fail), and delegates reading and aggregation to the reader and processor.
/// Invoked by the Worker when a queue message is consumed. No interface: the varying parts
/// (reading, processing) are abstracted behind their own interfaces.
/// </summary>
public sealed class ProcessImportHandler
{
    private readonly IImportJobRepository _importJobRepository;
    private readonly IImportRecordReader _recordReader;
    private readonly IImportProcessor _importProcessor;
    private readonly IOutboxRepository _outboxRepository;
    private readonly ILogger<ProcessImportHandler> _logger;

    public ProcessImportHandler(
        IImportJobRepository importJobRepository,
        IImportRecordReader recordReader,
        IImportProcessor importProcessor,
        IOutboxRepository outboxRepository,
        ILogger<ProcessImportHandler> logger)
    {
        _importJobRepository = importJobRepository;
        _recordReader = recordReader;
        _importProcessor = importProcessor;
        _outboxRepository = outboxRepository;
        _logger = logger;
    }

    // How many times to retry opening + reading + ingesting the file when the download stream fails
    // transiently (e.g. the object store resets the connection mid-read). Each attempt re-opens the
    // stream and re-ingests from scratch; this is safe because ingestion is idempotent per import
    // (staging is truncated before each COPY).
    private const int MaxDownloadAttempts = 3;

    /// <summary>
    /// Processes the import identified by <paramref name="importJobId"/>. <paramref name="openStream"/>
    /// opens the source file (re-invocable so a transient download failure can be retried from the
    /// start). On persistent failure, marks the job as failed and rethrows so the queue can retry
    /// (idempotent reprocessing).
    /// </summary>
    public async Task HandleAsync(
        Guid importJobId,
        Func<CancellationToken, Task<Stream>> openStream,
        CancellationToken cancellationToken = default)
    {
        // Correlate every log emitted while handling this import (here, the processor, the reader)
        // with the import job id, so the whole operation can be traced end to end. The message-template
        // form is rendered by the console logger and exposes ImportJobId as a structured field.
        using var logScope = _logger.BeginScope("ImportJobId:{ImportJobId}", importJobId);

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
            var result = await IngestWithRetryAsync(importJob.Id, openStream, cancellationToken);

            importJob.Complete(result.AggregatedDebtors, result.AggregatedEntities);

            // Transactional Outbox: stage the completion notification so it is committed atomically
            // with the job completion (same SaveChanges). The dispatcher delivers it afterwards.
            var payload = new ImportCompletedPayload(
                importJob.Id,
                importJob.FileName,
                result.ProcessedRecords,
                result.AggregatedDebtors,
                result.AggregatedEntities,
                importJob.Duration?.TotalSeconds,
                importJob.FinishedAt ?? DateTimeOffset.UtcNow);
            var outboxMessage = OutboxMessage.Create(importJob.Id, NotificationType.Email, payload.ToJson());
            await _outboxRepository.AddAsync(outboxMessage, cancellationToken);

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

    // Opens the stream and ingests it, retrying from scratch on a transient download/IO failure
    // (connection reset mid-read is common with large objects). Ingestion is idempotent per import,
    // so re-running is safe. Non-IO errors (e.g. bad data) are not retried here.
    private async Task<ImportResult> IngestWithRetryAsync(
        Guid importJobId,
        Func<CancellationToken, Task<Stream>> openStream,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var stream = await openStream(cancellationToken);
                var records = _recordReader.ReadAsync(stream, cancellationToken);
                return await _importProcessor.ProcessAsync(importJobId, records, cancellationToken);
            }
            catch (IOException ex) when (attempt < MaxDownloadAttempts)
            {
                // Transient I/O failure reading the download (e.g. connection reset). Back off and
                // retry the whole ingest from the start.
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)); // 1s, 2s, ...
                _logger.LogWarning(ex,
                    "Transient I/O error ingesting import {ImportJobId} (attempt {Attempt}/{Max}); retrying in {Delay}.",
                    importJobId, attempt, MaxDownloadAttempts, delay);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
