using Microsoft.Extensions.Logging.Abstractions;
using WayniBcra.Application.Imports;
using WayniBcra.Infrastructure.Imports;
using WayniBcra.Infrastructure.Persistence.Repositories;

namespace WayniBcra.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="DebtorReadRepository"/> against a real PostgreSQL (Testcontainers).
/// Focuses on the optional filter by situación of the top-N query: the WHERE runs over the persisted
/// original BCRA code, independent of the severity ranking used during aggregation.
/// </summary>
public sealed class DebtorReadRepositoryTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public DebtorReadRepositoryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async IAsyncEnumerable<BcraRecord> ToAsync(IEnumerable<BcraRecord> records)
    {
        foreach (var record in records)
        {
            yield return record;
        }

        await Task.CompletedTask;
    }

    private static BcraRecord Record(string idNumber, string creditStatus, decimal amount) =>
        new()
        {
            EntityCode = "00007",
            IdentificationType = "11",
            IdentificationNumber = idNumber,
            CreditStatus = creditStatus,
            FinancingAmount = amount
        };

    /// <summary>
    /// Seeds debtors with distinct max credit statuses (one of them an uncatalogued code, "99")
    /// through the real aggregation pipeline, so the persisted data matches production.
    /// </summary>
    private async Task SeedAsync()
    {
        var records = new[]
        {
            Record("30000000001", "05", 900.0m), // Irrecuperable
            Record("30000000002", "05", 300.0m), // Irrecuperable (lower amount)
            Record("30000000003", "01", 500.0m), // Normal
            Record("30000000004", "99", 400.0m), // uncatalogued code, persisted verbatim
        };

        await using var dbContext = _fixture.CreateDbContext();
        var processor = new NpgsqlImportProcessor(dbContext, NullLogger<NpgsqlImportProcessor>.Instance);
        await processor.ProcessAsync(Guid.NewGuid(), ToAsync(records));
    }

    [Fact]
    public async Task GetTop_WithoutFilter_ReturnsAllOrderedByFinancingDesc()
    {
        await SeedAsync();

        await using var dbContext = _fixture.CreateDbContext();
        var repository = new DebtorReadRepository(dbContext);

        var result = await repository.GetTopByTotalFinancingAsync(10);

        // The four seeded debtors, highest financing first.
        Assert.Equal(4, result.Count);
        Assert.Equal("30000000001", result[0].IdentificationNumber); // 900
        Assert.True(result[0].TotalFinancingAmount >= result[^1].TotalFinancingAmount);
    }

    [Fact]
    public async Task GetTop_WithSituacionFilter_ReturnsOnlyMatchingStatus()
    {
        await SeedAsync();

        await using var dbContext = _fixture.CreateDbContext();
        var repository = new DebtorReadRepository(dbContext);

        var result = await repository.GetTopByTotalFinancingAsync(10, "05");

        // Only the two debtors whose max credit status is exactly "05", ordered by financing desc.
        Assert.Equal(2, result.Count);
        Assert.All(result, d => Assert.Equal("05", d.MaxCreditStatus));
        Assert.Equal("30000000001", result[0].IdentificationNumber); // 900 before 300
        Assert.Equal("30000000002", result[1].IdentificationNumber);
    }

    [Fact]
    public async Task GetTop_WithUncataloguedCode_ReturnsThatDebtor()
    {
        await SeedAsync();

        await using var dbContext = _fixture.CreateDbContext();
        var repository = new DebtorReadRepository(dbContext);

        // "99" is not in the severity ranking, but it is persisted verbatim, so the filter finds it.
        var result = await repository.GetTopByTotalFinancingAsync(10, "99");

        Assert.Single(result);
        Assert.Equal("30000000004", result[0].IdentificationNumber);
        Assert.Equal("99", result[0].MaxCreditStatus);
    }

    [Fact]
    public async Task GetTop_WithNonPersistedStatus_ReturnsEmpty()
    {
        await SeedAsync();

        await using var dbContext = _fixture.CreateDbContext();
        var repository = new DebtorReadRepository(dbContext);

        // "-1" is the ranking fallback value, never persisted as a status; the filter matches nothing.
        var result = await repository.GetTopByTotalFinancingAsync(10, "-1");

        Assert.Empty(result);
    }
}
