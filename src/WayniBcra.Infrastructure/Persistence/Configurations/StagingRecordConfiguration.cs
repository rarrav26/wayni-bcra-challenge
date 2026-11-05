using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WayniBcra.Domain;

namespace WayniBcra.Infrastructure.Persistence.Configurations;

public sealed class StagingRecordConfiguration : IEntityTypeConfiguration<StagingRecord>
{
    public void Configure(EntityTypeBuilder<StagingRecord> builder)
    {
        builder.ToTable("staging_records");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.EntityCode).HasMaxLength(5).IsRequired();
        builder.Property(r => r.IdentificationType).HasMaxLength(2).IsRequired();
        builder.Property(r => r.IdentificationNumber).HasMaxLength(11).IsRequired();
        builder.Property(r => r.CreditStatus).HasMaxLength(2).IsRequired();

        // Per-record value: faithful to the layout (11 integer digits + 1 decimal).
        builder.Property(r => r.FinancingAmount).HasPrecision(12, 1);

        // Records are isolated and cleared per import (ADR-018).
        builder.HasIndex(r => r.ImportJobId);
    }
}
