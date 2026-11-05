using Microsoft.Extensions.Logging;
using WayniBcra.Domain;

namespace WayniBcra.Application.Imports;

/// <summary>
/// Handles a file upload on the producer side (API): stores the file, creates the ImportJob (Pending),
/// and publishes a queue message for the Worker to process it asynchronously.
/// </summary>
public sealed class UploadImportHandler
{
    private readonly IFileStorage _fileStorage;
    private readonly IImportQueuePublisher _queuePublisher;
    private readonly IImportJobRepository _importJobRepository;
    private readonly ILogger<UploadImportHandler> _logger;

    public UploadImportHandler(
        IFileStorage fileStorage,
        IImportQueuePublisher queuePublisher,
        IImportJobRepository importJobRepository,
        ILogger<UploadImportHandler> logger)
    {
        _fileStorage = fileStorage;
        _queuePublisher = queuePublisher;
        _importJobRepository = importJobRepository;
        _logger = logger;
    }

    /// <summary>
    /// Stores the file, registers the import, and enqueues it. Returns the created import job id.
    /// </summary>
    public async Task<Guid> HandleAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        var objectKey = await _fileStorage.UploadAsync(fileName, content, cancellationToken);

        var importJob = ImportJob.Create(fileName);
        await _importJobRepository.AddAsync(importJob, cancellationToken);
        await _importJobRepository.SaveChangesAsync(cancellationToken);

        await _queuePublisher.PublishAsync(
            new ImportQueueMessage { ImportJobId = importJob.Id, ObjectKey = objectKey },
            cancellationToken);

        _logger.LogInformation(
            "Upload registered: import {ImportJobId} for file {FileName} (object {ObjectKey}).",
            importJob.Id, fileName, objectKey);

        return importJob.Id;
    }
}
