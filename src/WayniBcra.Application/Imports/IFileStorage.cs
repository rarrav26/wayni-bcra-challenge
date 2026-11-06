namespace WayniBcra.Application.Imports;

/// <summary>
/// Stores and retrieves uploaded files (implemented over S3). Abstracts the storage origin so the
/// application does not depend on AWS directly.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Creates a pre-signed URL that lets the client upload (HTTP PUT) directly to storage, without
    /// the file passing through the API. Used for large files (the BCRA padrón is several GB).
    /// </summary>
    /// <param name="objectKey">The key the client will upload to.</param>
    /// <param name="expiresIn">How long the URL remains valid.</param>
    Task<string> CreateUploadUrlAsync(string objectKey, TimeSpan expiresIn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a read stream for a previously stored file.
    /// </summary>
    Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken = default);
}
