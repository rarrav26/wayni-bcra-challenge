using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WayniBcra.Application.Imports;
using WayniBcra.Application.Notifications;
using WayniBcra.Application.Queries;
using WayniBcra.Infrastructure.Aws;
using WayniBcra.Infrastructure.Imports;
using WayniBcra.Infrastructure.Notifications;
using WayniBcra.Infrastructure.Persistence;
using WayniBcra.Infrastructure.Persistence.Repositories;

namespace WayniBcra.Infrastructure;

/// <summary>
/// Registers Infrastructure services (persistence, file reading, AWS S3/SQS, email/outbox) into the
/// DI container. Called from the API and Worker composition roots.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres");

        services.AddDbContext<WayniBcraDbContext>(options =>
            options
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IImportRecordReader, BcraImportRecordReader>();
        services.AddScoped<IImportProcessor, NpgsqlImportProcessor>();
        services.AddScoped<IImportJobRepository, ImportJobRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IDebtorReadRepository, DebtorReadRepository>();
        services.AddScoped<IFinancialEntityReadRepository, FinancialEntityReadRepository>();

        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        AddAwsServices(services, configuration);

        return services;
    }

    private static void AddAwsServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AwsOptions>(configuration.GetSection(AwsOptions.SectionName));

        var awsOptions = configuration.GetSection(AwsOptions.SectionName).Get<AwsOptions>() ?? new AwsOptions();

        // Credentials: for LocalStack they are dummy; read from env/secrets, fallback to the
        // conventional "test"/"test" accepted by LocalStack.
        var accessKey = configuration["Aws:AccessKey"] ?? "test";
        var secretKey = configuration["Aws:SecretKey"] ?? "test";
        var credentials = new BasicAWSCredentials(accessKey, secretKey);

        services.AddSingleton<IAmazonS3>(_ =>
        {
            var config = new AmazonS3Config { AuthenticationRegion = awsOptions.Region };
            if (!string.IsNullOrWhiteSpace(awsOptions.ServiceUrl))
            {
                config.ServiceURL = awsOptions.ServiceUrl;
                config.ForcePathStyle = true; // Required by LocalStack for S3.
            }

            return new AmazonS3Client(credentials, config);
        });

        // Separate S3 client used ONLY to sign pre-signed URLs for the browser, against the public
        // endpoint (PublicServiceUrl, e.g. localhost:4566). Falls back to the internal ServiceUrl.
        services.AddSingleton<IS3Presigner>(_ =>
        {
            var publicUrl = awsOptions.PublicServiceUrl ?? awsOptions.ServiceUrl;
            var useHttp = !string.IsNullOrWhiteSpace(publicUrl)
                && publicUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
            var config = new AmazonS3Config { AuthenticationRegion = awsOptions.Region };
            if (!string.IsNullOrWhiteSpace(publicUrl))
            {
                config.ServiceURL = publicUrl;
                config.ForcePathStyle = true;
                config.UseHttp = useHttp;
            }

            // The URL scheme is forced per-request (GetPreSignedUrlRequest.Protocol); UseHttp is passed
            // so S3FileStorage can set it. LocalStack serves plain HTTP, so https:// would fail TLS.
            return new S3Presigner(new AmazonS3Client(credentials, config), useHttp);
        });

        services.AddSingleton<IAmazonSQS>(_ =>
        {
            var config = new AmazonSQSConfig { AuthenticationRegion = awsOptions.Region };
            if (!string.IsNullOrWhiteSpace(awsOptions.ServiceUrl))
            {
                config.ServiceURL = awsOptions.ServiceUrl;
            }

            return new AmazonSQSClient(credentials, config);
        });

        services.AddScoped<IFileStorage, S3FileStorage>();
        services.AddScoped<IImportQueuePublisher, SqsImportQueuePublisher>();
    }
}
