using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WayniBcra.Domain;

namespace WayniBcra.Infrastructure.Persistence.Configurations;

public sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.ToTable("import_jobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id)
            .ValueGeneratedNever(); // Guid generated in the domain, not by the DB.

        builder.Property(j => j.FileName)
            .HasMaxLength(260)
            .IsRequired();

        // Enum stored as its underlying int.
        builder.Property(j => j.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(j => j.CreatedAt).IsRequired();
        builder.Property(j => j.StartedAt);
        builder.Property(j => j.FinishedAt);
        builder.Property(j => j.ProcessedDebtors);
        builder.Property(j => j.ProcessedEntities);
        builder.Property(j => j.ErrorMessage).HasMaxLength(2000);
    }
}
