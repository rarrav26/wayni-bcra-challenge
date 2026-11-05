namespace WayniBcra.Application.Imports;

/// <summary>
/// Stores and retrieves uploaded files (implemented over S3). Abstracts the storage origin so the
/// application does not depend on AWS directly.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Uploads a file and returns the storage key under which it was stored.
    /// </summary>
    Task<string> UploadAsync(string fileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a read stream for a previously stored file.
    /// </summary>
    Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken = default);
}
