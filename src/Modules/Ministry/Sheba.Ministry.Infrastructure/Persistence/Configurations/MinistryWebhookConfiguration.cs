using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.Ministry.Domain.Entities;

namespace Sheba.Ministry.Infrastructure.Persistence.Configurations;

internal sealed class MinistryWebhookConfiguration : IEntityTypeConfiguration<MinistryWebhook>
{
    public void Configure(EntityTypeBuilder<MinistryWebhook> builder)
    {
        builder.ToTable("ministry_webhooks");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(w => w.MinistryId).HasColumnName("ministry_id").IsRequired();
        builder.Property(w => w.EndpointId).HasColumnName("endpoint_id");
        builder.Property(w => w.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
        builder.Property(w => w.ShebaWebhookPath).HasColumnName("sheba_webhook_path").IsRequired();
        builder.Property(w => w.SigningSecret).HasColumnName("signing_secret").IsRequired();
        builder.Property(w => w.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(w => w.LastReceivedAt).HasColumnName("last_received_at");
        builder.Property(w => w.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Ignore(w => w.DomainEvents);
    }
}
