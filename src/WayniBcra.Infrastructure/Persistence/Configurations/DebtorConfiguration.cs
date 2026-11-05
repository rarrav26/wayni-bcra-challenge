using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WayniBcra.Domain;

namespace WayniBcra.Infrastructure.Persistence.Configurations;

public sealed class DebtorConfiguration : IEntityTypeConfiguration<Debtor>
{
    public void Configure(EntityTypeBuilder<Debtor> builder)
    {
        builder.ToTable("debtors");

        builder.HasKey(d => d.IdentificationNumber);

        builder.Property(d => d.IdentificationNumber)
            .HasMaxLength(11)
            .IsRequired();

        // 2-digit BCRA status code, stored as string (ADR-013).
        builder.Property(d => d.MaxCreditStatus)
            .HasMaxLength(2)
            .IsRequired();

        // Aggregate sum: generous precision (ADR, numeric(20,1)).
        builder.Property(d => d.TotalFinancingAmount)
            .HasPrecision(20, 1);

        // Supports GET /deudores/top/{n} (order by total desc).
        builder.HasIndex(d => d.TotalFinancingAmount)
            .IsDescending(true);
    }
}
