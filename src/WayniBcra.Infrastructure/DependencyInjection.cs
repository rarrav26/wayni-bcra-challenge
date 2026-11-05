using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WayniBcra.Application.Imports;
using WayniBcra.Application.Queries;
using WayniBcra.Infrastructure.Imports;
using WayniBcra.Infrastructure.Persistence;
using WayniBcra.Infrastructure.Persistence.Repositories;

namespace WayniBcra.Infrastructure;

/// <summary>
/// Registers Infrastructure services (persistence, file reading) into the DI container.
/// Called from the API and Worker composition roots.
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
        services.AddScoped<IDebtorReadRepository, DebtorReadRepository>();
        services.AddScoped<IFinancialEntityReadRepository, FinancialEntityReadRepository>();

        return services;
    }
}
