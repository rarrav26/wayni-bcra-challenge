using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using WayniBcra.Api.HealthChecks;
using WayniBcra.Application;
using WayniBcra.Infrastructure;
using WayniBcra.Infrastructure.Logging;
using WayniBcra.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Centralize logs in CloudWatch (in addition to the console) under the "api" stream.
builder.Logging.AddCloudWatchLogging(builder.Configuration, "api");

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// CORS for the frontend (React + Vite). Allowed origins come from config
// (Cors:AllowedOrigins as an array; in Docker via Cors__AllowedOrigins__0, ...),
// defaulting to the local Vite dev server.
const string FrontendCorsPolicy = "FrontendCorsPolicy";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// Health checks exposed at /health:
//  - PostgreSQL uses the community AspNetCore.HealthChecks.NpgSql package (the standard choice).
//  - S3 and SQS use custom checks (S3HealthCheck / SqsHealthCheck) for a uniform approach: both
//    reuse the IAmazonS3 / IAmazonSQS clients already registered by Infrastructure (configured with
//    the LocalStack endpoint locally, real AWS in production). The community SQS package can't target
//    a custom ServiceURL, so a custom check was required there anyway; S3 follows the same pattern.
builder.Services.AddHealthChecks()
    .AddNpgSql(
        sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("Postgres")!,
        name: "postgres")
    .AddCheck<S3HealthCheck>("s3")
    .AddCheck<SqsHealthCheck>("sqs");

var app = builder.Build();

// Apply pending migrations at startup so `docker-compose up` leaves the schema ready.
// Skipped in the Testing environment (API tests replace the data layer and need no real database).
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<WayniBcraDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        // Point Swagger UI to the OpenAPI document produced by MapOpenApi().
        options.SwaggerEndpoint("/openapi/v1.json", "WayniBcra.Api v1");
    });

    // Convenience: redirect the root to the Swagger UI in development.
    app.MapGet("/", () => Results.Redirect("/swagger"));
}

app.UseCors(FrontendCorsPolicy);

app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    // Return a JSON body with the aggregate status and a per-dependency breakdown.
    ResponseWriter = HealthCheckResponseWriter.WriteResponse
});
app.MapControllers();

app.Run();

// Exposed so WebApplicationFactory<Program> can reference the API entry point in tests.
public partial class Program { }
