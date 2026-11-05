using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using WayniBcra.Application.Imports;

namespace WayniBcra.Infrastructure.Aws;

/// <summary>
/// S3-backed file storage. Uploads files under a unique key and downloads them by key.
/// The bucket is assumed to exist (provisioned by the LocalStack init script, ADR-026).
/// </summary>
public sealed class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3;
    private readonly AwsOptions _options;

    public S3FileStorage(IAmazonS3 s3, IOptions<AwsOptions> options)
    {
        _s3 = s3;
        _options = options.Value;
    }

    public async Task<string> UploadAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        // Unique key per upload to avoid collisions between imports; keeps the original file name as suffix.
        var objectKey = $"imports/{Guid.NewGuid():N}/{fileName}";

        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            InputStream = content,
            AutoCloseStream = false
        };

        await _s3.PutObjectAsync(request, cancellationToken);
        return objectKey;
    }

    public async Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var response = await _s3.GetObjectAsync(_options.BucketName, objectKey, cancellationToken);

        // Copy to a seekable MemoryStream so the caller can read it freely and the S3 response is released.
        var memory = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        return memory;
    }
}
