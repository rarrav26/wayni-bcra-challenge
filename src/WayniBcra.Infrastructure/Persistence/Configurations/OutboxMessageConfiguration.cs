using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WayniBcra.Domain;

namespace WayniBcra.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .ValueGeneratedNever(); // Guid generated in the domain, not by the DB.

        builder.Property(m => m.ImportJobId).IsRequired();

        // Enums stored as their underlying int, consistent with ImportJob.Status.
        builder.Property(m => m.NotificationType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(m => m.Status)
            .HasConversion<int>()
            .IsRequired();

        // Self-contained JSON payload (jsonb in PostgreSQL for efficient storage).
        builder.Property(m => m.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(m => m.Attempts).IsRequired();
        builder.Property(m => m.NextAttemptAt).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.SentAt);
        builder.Property(m => m.LastError).HasMaxLength(2000);

        // The dispatcher polls by (status, next_attempt_at); index supports that lookup.
        builder.HasIndex(m => new { m.Status, m.NextAttemptAt })
            .HasDatabaseName("ix_outbox_messages_status_next_attempt_at");
    }
}
