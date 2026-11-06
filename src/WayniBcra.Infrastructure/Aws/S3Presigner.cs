using Amazon.S3;

namespace WayniBcra.Infrastructure.Aws;

/// <summary>
/// Holds an S3 client configured against the PUBLIC endpoint, used only to sign pre-signed URLs that
/// are handed to the browser. Kept separate from the default <see cref="IAmazonS3"/> (internal
/// endpoint) because the browser cannot reach the internal Docker host (e.g. "localstack:4566"),
/// only the public one (e.g. "localhost:4566"), and the signature is bound to the endpoint host.
/// </summary>
public interface IS3Presigner
{
    IAmazonS3 Client { get; }

    /// <summary>Whether the public endpoint uses plain HTTP (LocalStack), so URLs are signed as http.</summary>
    bool UseHttp { get; }
}

public sealed class S3Presigner : IS3Presigner
{
    public S3Presigner(IAmazonS3 client, bool useHttp)
    {
        Client = client;
        UseHttp = useHttp;
    }

    public IAmazonS3 Client { get; }

    public bool UseHttp { get; }
}
