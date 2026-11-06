using Microsoft.Extensions.Logging;
using WayniBcra.Domain;

namespace WayniBcra.Application.Imports;

/// <summary>
/// Handles an upload request on the producer side (API). Instead of receiving the file, it registers
/// the import and returns a pre-signed URL so the client uploads the file directly to S3. Processing
/// is then triggered by the S3 object-created event (S3 -> SQS -> Worker), so large files (the BCRA
/// padrón is several GB) never pass through the API.
/// </summary>
public sealed class UploadImportHandler
{
    // The object key embeds the import job id so the S3 event can be correlated to the job.
    private static readonly TimeSpan UploadUrlLifetime = TimeSpan.FromMinutes(30);

    private readonly IFileStorage _fileStorage;
    private readonly IImportJobRepository _importJobRepository;
    private readonly ILogger<UploadImportHandler> _logger;

    public UploadImportHandler(
        IFileStorage fileStorage,
        IImportJobRepository importJobRepository,
        ILogger<UploadImportHandler> logger)
    {
        _fileStorage = fileStorage;
        _importJobRepository = importJobRepository;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new import and returns the id, the object key, and a pre-signed PUT URL the client
    /// uses to upload the file directly to storage.
    /// </summary>
    public async Task<UploadTicket> HandleAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.", nameof(fileName));
        }

        var importJob = ImportJob.Create(fileName);
        await _importJobRepository.AddAsync(importJob, cancellationToken);
        await _importJobRepository.SaveChangesAsync(cancellationToken);

        // The key embeds the import job id so the S3 object-created event can be correlated back to
        // the job (the S3 event carries only bucket + key, not our ids).
        var objectKey = ImportObjectKey.Build(importJob.Id, fileName);
        var uploadUrl = await _fileStorage.CreateUploadUrlAsync(objectKey, UploadUrlLifetime, cancellationToken);

        _logger.LogInformation(
            "Upload registered: import {ImportJobId} for file {FileName} (object {ObjectKey}). Awaiting client upload.",
            importJob.Id, fileName, objectKey);

        return new UploadTicket(importJob.Id, objectKey, uploadUrl);
    }
}
