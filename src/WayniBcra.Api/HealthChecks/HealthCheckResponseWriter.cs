using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WayniBcra.Api.HealthChecks;

/// <summary>
/// Writes the /health response as JSON with the aggregate status plus a per-dependency breakdown
/// (postgres, s3, sqs). Orchestrators only read the HTTP status code, but the detailed body makes
/// it clear which dependencies are verified and which one failed when the status is not Healthy.
/// </summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                duration = entry.Value.Duration.ToString(),
                // Only present when the check reports a reason (typically on failure).
                description = entry.Value.Description
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions));
    }
}
