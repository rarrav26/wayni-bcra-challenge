using Microsoft.Extensions.DependencyInjection;
using WayniBcra.Application.Imports;

namespace WayniBcra.Application;

/// <summary>
/// Registers Application-layer services (use cases) into the DI container.
/// Called from the API and Worker composition roots.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ProcessImportHandler>();

        return services;
    }
}
