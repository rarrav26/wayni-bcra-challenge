using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using WayniBcra.Infrastructure.Aws;

namespace WayniBcra.Api.HealthChecks;

/// <summary>
/// Health check for S3 connectivity and import-bucket availability.
/// </summary>
/// <remarks>
/// A custom check (instead of the community AspNetCore.HealthChecks.Aws.S3 package) keeps both AWS
/// checks uniform: S3 and SQS follow the same pattern and both reuse the clients already registered
/// in DI (configured with the LocalStack endpoint locally, real AWS in production). It also sidesteps
/// the SQS package limitation that forced a custom SQS check (no custom ServiceURL support).
/// </remarks>
public sealed class S3HealthCheck : IHealthCheck
{
    private readonly IAmazonS3 _s3;
    private readonly AwsOptions _options;

    public S3HealthCheck(IAmazonS3 s3, IOptions<AwsOptions> options)
    {
        _s3 = s3;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // A lightweight HEAD on the bucket verifies connectivity and that the import bucket exists.
            await _s3.GetBucketLocationAsync(
                new GetBucketLocationRequest { BucketName = _options.BucketName },
                cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"S3 bucket '{_options.BucketName}' is not reachable.", ex);
        }
    }
}
