using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WayniBcra.Application.Imports;
using WayniBcra.Infrastructure.Imports;

namespace WayniBcra.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="NpgsqlImportProcessor"/> against a real PostgreSQL (Testcontainers).
/// Validates the COPY ingestion and the SQL aggregation: financing sums and the credit-status severity
/// ranking (ADR-014), including the unknown-code fallback (ADR-019).
/// </summary>
public sealed class NpgsqlImportProcessorTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public NpgsqlImportProcessorTests(PostgresFixture fixture)
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

    private static BcraRecord Record(string entityCode, string idNumber, string creditStatus, decimal amount) =>
        new()
        {
            EntityCode = entityCode,
            IdentificationType = "11",
            IdentificationNumber = idNumber,
            CreditStatus = creditStatus,
            FinancingAmount = amount
        };

    [Fact]
    public async Task ProcessAsync_AggregatesDebtorsWithSeverityRankingAndSums()
    {
        var importJobId = Guid.NewGuid();
        var records = new[]
        {
            // Debtor A: 05 (Irrecuperable) beats 21 (Riesgo Bajo). Sum 100 + 50 = 150.
            Record("00007", "20111111111", "05", 100.0m),
            Record("00011", "20111111111", "21", 50.0m),
            // Debtor B: 01 (Normal) beats 11 (special). Sum 10 + 5 = 15.
            Record("00007", "20222222222", "11", 10.0m),
            Record("00007", "20222222222", "01", 5.0m),
            // Debtor C: only 11 -> stays 11. Sum 7.
            Record("00011", "20333333333", "11", 7.0m),
            // Debtor D: unknown code 47 (priority -1) loses to 03. Sum 3 + 2 = 5.
            Record("00007", "20444444444", "47", 3.0m),
            Record("00007", "20444444444", "03", 2.0m),
        };

        await using var dbContext = _fixture.CreateDbContext();
        var processor = new NpgsqlImportProcessor(dbContext, NullLogger<NpgsqlImportProcessor>.Instance);

        var result = await processor.ProcessAsync(importJobId, ToAsync(records));

        Assert.Equal(7, result.ProcessedRecords);
        Assert.Equal(4, result.AggregatedDebtors);

        await using var assertContext = _fixture.CreateDbContext();

        var debtorA = await assertContext.Debtors.AsNoTracking().SingleAsync(d => d.IdentificationNumber == "20111111111");
        Assert.Equal("05", debtorA.MaxCreditStatus);
        Assert.Equal(150.0m, debtorA.TotalFinancingAmount);

        var debtorB = await assertContext.Debtors.AsNoTracking().SingleAsync(d => d.IdentificationNumber == "20222222222");
        Assert.Equal("01", debtorB.MaxCreditStatus);
        Assert.Equal(15.0m, debtorB.TotalFinancingAmount);

        var debtorC = await assertContext.Debtors.AsNoTracking().SingleAsync(d => d.IdentificationNumber == "20333333333");
        Assert.Equal("11", debtorC.MaxCreditStatus);
        Assert.Equal(7.0m, debtorC.TotalFinancingAmount);

        var debtorD = await assertContext.Debtors.AsNoTracking().SingleAsync(d => d.IdentificationNumber == "20444444444");
        Assert.Equal("03", debtorD.MaxCreditStatus);
        Assert.Equal(5.0m, debtorD.TotalFinancingAmount);
    }

    [Fact]
    public async Task ProcessAsync_AggregatesEntityTotals()
    {
        var importJobId = Guid.NewGuid();
        var records = new[]
        {
            Record("90001", "20111111111", "01", 100.0m),
            Record("90001", "20222222222", "01", 25.5m),
            Record("90002", "20333333333", "01", 10.0m),
        };

        await using var dbContext = _fixture.CreateDbContext();
        var processor = new NpgsqlImportProcessor(dbContext, NullLogger<NpgsqlImportProcessor>.Instance);

        var result = await processor.ProcessAsync(importJobId, ToAsync(records));

        Assert.Equal(3, result.ProcessedRecords);

        await using var assertContext = _fixture.CreateDbContext();

        var entity1 = await assertContext.FinancialEntities.AsNoTracking().SingleAsync(e => e.EntityCode == "90001");
        Assert.Equal(125.5m, entity1.TotalFinancingAmount);

        var entity2 = await assertContext.FinancialEntities.AsNoTracking().SingleAsync(e => e.EntityCode == "90002");
        Assert.Equal(10.0m, entity2.TotalFinancingAmount);
    }

    [Fact]
    public async Task ProcessAsync_IsIdempotent_WhenRunTwice()
    {
        var importJobId = Guid.NewGuid();
        var records = new[]
        {
            Record("80001", "20999999999", "04", 42.0m),
        };

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var processor = new NpgsqlImportProcessor(dbContext, NullLogger<NpgsqlImportProcessor>.Instance);
            await processor.ProcessAsync(importJobId, ToAsync(records));
        }

        // Re-run the same import: upsert must keep a single, consistent result.
        await using (var dbContext = _fixture.CreateDbContext())
        {
            var processor = new NpgsqlImportProcessor(dbContext, NullLogger<NpgsqlImportProcessor>.Instance);
            await processor.ProcessAsync(importJobId, ToAsync(records));
        }

        await using var assertContext = _fixture.CreateDbContext();
        var debtor = await assertContext.Debtors.AsNoTracking().SingleAsync(d => d.IdentificationNumber == "20999999999");
        Assert.Equal("04", debtor.MaxCreditStatus);
        Assert.Equal(42.0m, debtor.TotalFinancingAmount);
    }
}
