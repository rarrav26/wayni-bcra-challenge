using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using WayniBcra.Application.Queries;

namespace WayniBcra.ApiTests;

/// <summary>
/// Spins up the API in-memory for testing, in the "Testing" environment (which skips the startup
/// migration). The read repositories are replaced by NSubstitute fakes so the tests exercise the
/// HTTP contract (routing, status codes, JSON shape) without needing a real database.
/// </summary>
public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    public IDebtorReadRepository DebtorRepository { get; } = Substitute.For<IDebtorReadRepository>();
    public IFinancialEntityReadRepository FinancialEntityRepository { get; } = Substitute.For<IFinancialEntityReadRepository>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace the real read repositories with the fakes.
            services.RemoveAll<IDebtorReadRepository>();
            services.RemoveAll<IFinancialEntityReadRepository>();
            services.AddScoped(_ => DebtorRepository);
            services.AddScoped(_ => FinancialEntityRepository);
        });
    }
}
