using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WayniBcra.Infrastructure.Persistence;

namespace WayniBcra.IntegrationTests;

/// <summary>
/// Spins up an ephemeral PostgreSQL container for integration tests, applies the EF Core schema,
/// and provides configured <see cref="WayniBcraDbContext"/> instances. The container is created once
/// per test class and disposed afterwards.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("wayni_bcra")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Apply the schema using the same migrations as production.
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public WayniBcraDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WayniBcraDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new WayniBcraDbContext(options);
    }
}
