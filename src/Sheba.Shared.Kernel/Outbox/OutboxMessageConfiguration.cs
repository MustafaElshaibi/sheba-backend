using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sheba.Shared.Kernel.Outbox;

/// <summary>
/// Applied explicitly by every module's DbContext.OnModelCreating (not assembly-scanned, since
/// this type lives in Shared.Kernel, outside each module's own configuration assembly). Maps
/// into the module's own default schema — one outbox_messages table per module, never shared.
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");

        builder.Property(m => m.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(m => m.AggregateType).HasColumnName("aggregate_type").HasMaxLength(200).IsRequired();
        builder.Property(m => m.AggregateId).HasColumnName("aggregate_id").IsRequired();
        builder.Property(m => m.EventType).HasColumnName("event_type").HasMaxLength(500).IsRequired();
        builder.Property(m => m.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(m => m.Attempts).HasColumnName("attempts").IsRequired();
        builder.Property(m => m.NextAttemptAt).HasColumnName("next_attempt_at").IsRequired();
        builder.Property(m => m.PublishedAt).HasColumnName("published_at");
        builder.Property(m => m.LastError).HasColumnName("last_error").HasMaxLength(2000);

        // The dispatcher's poll query filters on (status, next_attempt_at) across every module.
        builder.HasIndex(m => new { m.Status, m.NextAttemptAt }).HasDatabaseName("ix_outbox_messages_status_next_attempt");
    }
}
