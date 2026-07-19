using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.Notification.Domain.Entities;

namespace Sheba.Notification.Infrastructure.Persistence.Configurations;

internal sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> b)
    {
        b.ToTable("notification_templates");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(e => e.TemplateKey).HasColumnName("template_key").HasMaxLength(100).IsRequired();
        b.HasIndex(e => e.TemplateKey).IsUnique();
        b.Property(e => e.SubjectEn).HasColumnName("subject_en").IsRequired();
        b.Property(e => e.SubjectAr).HasColumnName("subject_ar").IsRequired();
        b.Property(e => e.BodyHtmlEn).HasColumnName("body_html_en").IsRequired();
        b.Property(e => e.BodyHtmlAr).HasColumnName("body_html_ar").IsRequired();
        b.Property(e => e.BodyTextEn).HasColumnName("body_text_en").IsRequired();
        b.Property(e => e.BodyTextAr).HasColumnName("body_text_ar").IsRequired();
        b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Ignore(e => e.DomainEvents);
    }
}
