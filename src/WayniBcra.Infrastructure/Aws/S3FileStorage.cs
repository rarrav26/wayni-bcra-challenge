using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using WayniBcra.Application.Imports;

namespace WayniBcra.Infrastructure.Aws;

/// <summary>
/// S3-backed file storage. Uploads files under a unique key and downloads them by key.
/// The bucket is assumed to exist (provisioned by the LocalStack init script).
/// </summary>
public sealed class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3;
    private readonly IS3Presigner _presigner;
    private readonly AwsOptions _options;

    public S3FileStorage(IAmazonS3 s3, IS3Presigner presigner, IOptions<AwsOptions> options)
    {
        _s3 = s3;
        _presigner = presigner;
        _options = options.Value;
    }

    public Task<string> CreateUploadUrlAsync(
        string objectKey, TimeSpan expiresIn, CancellationToken cancellationToken = default)
    {
        // Pre-signed PUT URL: the client uploads directly to S3, so large files never pass through
        // the API. The URL grants only PUT on this specific key, and expires. Signed against the
        // PUBLIC endpoint so the browser (which cannot resolve the internal Docker host) can use it.
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiresIn),
            // Force the URL scheme to match the public endpoint. LocalStack serves plain HTTP; without
            // this the SDK signs https:// and the browser's TLS handshake fails.
            Protocol = _presigner.UseHttp ? Protocol.HTTP : Protocol.HTTPS,
        };

        return _presigner.Client.GetPreSignedURLAsync(request);
    }

    public async Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var response = await _s3.GetObjectAsync(_options.BucketName, objectKey, cancellationToken);

        // Return the S3 response stream directly (do NOT buffer it into memory): the reader consumes
        // it line by line as S3 transmits it, keeping memory bounded regardless of file size (the
        // BCRA padrón is ~6 GB). Disposing this stream releases the underlying HTTP response.
        return response.ResponseStream;
    }
}
