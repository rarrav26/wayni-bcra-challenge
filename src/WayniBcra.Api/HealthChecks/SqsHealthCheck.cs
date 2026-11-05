using Amazon.SQS;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using WayniBcra.Infrastructure.Aws;

namespace WayniBcra.Api.HealthChecks;

/// <summary>
/// Health check for SQS connectivity and import-queue availability.
/// </summary>
/// <remarks>
/// Custom check (instead of the community AspNetCore.HealthChecks.Aws.Sqs package) for two reasons:
/// that package's options expose only credentials/region and cannot target a custom <c>ServiceURL</c>
/// endpoint (so it can't point at LocalStack), and keeping S3 and SQS checks custom gives a uniform
/// approach for both AWS services. It reuses the <see cref="IAmazonSQS"/> client already registered
/// in DI (LocalStack endpoint locally, real AWS in production), mirroring <see cref="S3HealthCheck"/>.
/// </remarks>
public sealed class SqsHealthCheck : IHealthCheck
{
    private readonly IAmazonSQS _sqs;
    private readonly AwsOptions _options;

    public SqsHealthCheck(IAmazonSQS sqs, IOptions<AwsOptions> options)
    {
        _sqs = sqs;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolving the queue URL verifies both connectivity to SQS and that the import
            // queue exists, equivalent to how the S3 check targets a specific bucket.
            await _sqs.GetQueueUrlAsync(_options.QueueName, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"SQS queue '{_options.QueueName}' is not reachable.", ex);
        }
    }
}
