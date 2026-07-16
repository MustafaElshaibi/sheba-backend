using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.ServiceRequest.Domain.Entities;

namespace Sheba.ServiceRequest.Infrastructure.Persistence.Configurations;

internal sealed class ServiceRequiredDocumentConfiguration : IEntityTypeConfiguration<ServiceRequiredDocument>
{
    public void Configure(EntityTypeBuilder<ServiceRequiredDocument> b)
    {
        b.ToTable("service_required_documents");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(e => e.ServiceId).HasColumnName("service_id").IsRequired();
        b.Property(e => e.DocumentType).HasColumnName("document_type").HasMaxLength(100).IsRequired();
        b.Property(e => e.NameAr).HasColumnName("name_ar").HasMaxLength(200).IsRequired();
        b.Property(e => e.NameEn).HasColumnName("name_en").HasMaxLength(200).IsRequired();
        b.Property(e => e.IsMandatory).HasColumnName("is_mandatory").HasDefaultValue(true);
        b.Property(e => e.MaxSizeMb).HasColumnName("max_size_mb").HasDefaultValue(5);
        b.Property(e => e.AllowedMimeTypesCsv).HasColumnName("allowed_mime_types");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Ignore(e => e.DomainEvents);
    }
}
