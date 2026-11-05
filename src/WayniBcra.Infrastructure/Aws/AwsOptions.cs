namespace WayniBcra.Infrastructure.Aws;

/// <summary>
/// Strongly-typed AWS configuration (bound from the "Aws" section). Credentials are NOT here:
/// they come from environment variables (Docker) or User Secrets (dev).
/// </summary>
public sealed class AwsOptions
{
    public const string SectionName = "Aws";

    /// <summary>LocalStack endpoint (e.g. http://localhost:4566). Empty/null means real AWS.</summary>
    public string? ServiceUrl { get; set; }

    /// <summary>AWS region. Required by the SDK even for LocalStack (which ignores it).</summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>S3 bucket where uploaded files are stored.</summary>
    public string BucketName { get; set; } = "wayni-bcra-imports";

    /// <summary>SQS queue for import messages.</summary>
    public string QueueName { get; set; } = "wayni-bcra-import-queue";
}
