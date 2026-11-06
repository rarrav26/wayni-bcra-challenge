using AWS.Logger;
using AWS.Logger.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WayniBcra.Infrastructure.Aws;

namespace WayniBcra.Infrastructure.Logging;

/// <summary>
/// Wires application logs to CloudWatch Logs (centralized across API + Worker) in addition to the
/// console. Enabled only when an AWS endpoint is configured (LocalStack in dev/Docker, or real AWS);
/// otherwise it is a no-op, so running without an endpoint still logs to stdout.
/// </summary>
public static class CloudWatchLogging
{
    /// <summary>
    /// Adds the CloudWatch logging provider for the given <paramref name="logStreamPrefix"/>
    /// (e.g. "api" or "worker"), so each service is a distinct stream within the shared log group.
    /// </summary>
    public static ILoggingBuilder AddCloudWatchLogging(
        this ILoggingBuilder logging, IConfiguration configuration, string logStreamPrefix)
    {
        var aws = configuration.GetSection(AwsOptions.SectionName).Get<AwsOptions>() ?? new AwsOptions();

        // Only enable when an endpoint is set (LocalStack/AWS). Keeps local `dotnet run` without
        // LocalStack logging to console only.
        if (string.IsNullOrWhiteSpace(aws.ServiceUrl))
        {
            return logging;
        }

        var config = new AWSLoggerConfig(aws.LogGroup)
        {
            Region = aws.Region,
            ServiceUrl = aws.ServiceUrl,
            // Dummy credentials accepted by LocalStack; real AWS would use the ambient credentials.
            Credentials = new Amazon.Runtime.BasicAWSCredentials(
                configuration["Aws:AccessKey"] ?? "test",
                configuration["Aws:SecretKey"] ?? "test"),
            LogStreamNamePrefix = logStreamPrefix,
        };

        logging.AddAWSProvider(config);
        return logging;
    }
}
