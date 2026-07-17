using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sheba.Shared.Kernel.Outbox;

/// <summary>Applied explicitly by every module's DbContext.OnModelCreating (see OutboxMessageConfiguration).</summary>
public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");

        builder.Property(m => m.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(m => m.ConsumerName).HasColumnName("consumer_name").HasMaxLength(200).IsRequired();
        builder.Property(m => m.ProcessedAt).HasColumnName("processed_at").IsRequired();

        builder.HasIndex(m => new { m.EventId, m.ConsumerName })
            .IsUnique()
            .HasDatabaseName("ux_inbox_messages_event_consumer");
    }
}
