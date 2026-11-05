using Microsoft.EntityFrameworkCore;
using WayniBcra.Application;
using WayniBcra.Infrastructure;
using WayniBcra.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

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

app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposed so WebApplicationFactory<Program> can reference the API entry point in tests.
public partial class Program { }
