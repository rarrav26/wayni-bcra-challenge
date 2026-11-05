using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WayniBcra.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by EF Core tooling (migrations) to instantiate the context without
/// the application's DI container. The connection string here is a placeholder: generating
/// migrations does not open a database connection.
/// </summary>
public sealed class WayniBcraDbContextFactory : IDesignTimeDbContextFactory<WayniBcraDbContext>
{
    public WayniBcraDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WayniBcraDbContext>()
            .UseNpgsql("Host=localhost;Database=wayni_bcra;Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new WayniBcraDbContext(options);
    }
}
