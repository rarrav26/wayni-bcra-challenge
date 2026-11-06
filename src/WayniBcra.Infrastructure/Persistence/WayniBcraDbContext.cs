using Microsoft.EntityFrameworkCore;
using WayniBcra.Domain;

namespace WayniBcra.Infrastructure.Persistence;

/// <summary>
/// EF Core database context. Maps the domain entities to PostgreSQL tables (snake_case via
/// EFCore.NamingConventions). Used for the model and queries; bulk ingestion uses Npgsql COPY.
/// </summary>
public sealed class WayniBcraDbContext : DbContext
{
    public WayniBcraDbContext(DbContextOptions<WayniBcraDbContext> options) : base(options)
    {
    }

    public DbSet<Debtor> Debtors => Set<Debtor>();
    public DbSet<FinancialEntity> FinancialEntities => Set<FinancialEntity>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<StagingRecord> StagingRecords => Set<StagingRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WayniBcraDbContext).Assembly);
    }
}
