using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WayniBcra.Domain;

namespace WayniBcra.Infrastructure.Persistence.Configurations;

public sealed class FinancialEntityConfiguration : IEntityTypeConfiguration<FinancialEntity>
{
    public void Configure(EntityTypeBuilder<FinancialEntity> builder)
    {
        builder.ToTable("financial_entities");

        builder.HasKey(e => e.EntityCode);

        builder.Property(e => e.EntityCode)
            .HasMaxLength(5)
            .IsRequired();

        builder.Property(e => e.TotalFinancingAmount)
            .HasPrecision(20, 1);
    }
}
