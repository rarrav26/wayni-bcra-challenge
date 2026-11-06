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

        // 2-digit BCRA status code, stored as string to preserve its original format.
        builder.Property(d => d.MaxCreditStatus)
            .HasMaxLength(2)
            .IsRequired();

        // Aggregate sum across all of a debtor's records: generous precision to avoid overflow.
        builder.Property(d => d.TotalFinancingAmount)
            .HasPrecision(20, 1);

        // Supports GET /deudores/top/{n} (order by total desc).
        builder.HasIndex(d => d.TotalFinancingAmount)
            .IsDescending(true);
    }
}
