using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sheba.Document.Infrastructure.Persistence.Configurations;

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Domain.Entities.Document>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Document> b)
    {
        b.ToTable("documents");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(e => e.OwnerId).HasColumnName("owner_id").IsRequired();
        b.HasIndex(e => e.OwnerId).HasDatabaseName("ix_documents_owner");
        b.Property(e => e.FileName).HasColumnName("file_name").HasMaxLength(500).IsRequired();
        b.Property(e => e.ContentType).HasColumnName("content_type").HasMaxLength(150).IsRequired();
        b.Property(e => e.SizeBytes).HasColumnName("size_bytes").IsRequired();
        b.Property(e => e.BucketName).HasColumnName("bucket_name").HasMaxLength(100).IsRequired();
        b.Property(e => e.ObjectKey).HasColumnName("object_key").HasMaxLength(500).IsRequired();
        b.Property(e => e.DocumentType).HasColumnName("document_type").HasMaxLength(50).IsRequired();
        b.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        b.Property(e => e.DeletedAt).HasColumnName("deleted_at");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Ignore(e => e.DomainEvents);
    }
}

