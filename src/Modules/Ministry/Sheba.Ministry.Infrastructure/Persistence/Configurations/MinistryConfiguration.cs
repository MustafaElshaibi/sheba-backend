using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sheba.Ministry.Infrastructure.Persistence.Configurations;

internal sealed class MinistryConfiguration : IEntityTypeConfiguration<Domain.Entities.Ministry>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Ministry> builder)
    {
        builder.ToTable("ministries");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(m => m.ParentMinistryId).HasColumnName("parent_ministry_id");
        builder.Property(m => m.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        builder.HasIndex(m => m.Code).IsUnique().HasDatabaseName("ix_ministries_code");
        builder.Property(m => m.NameAr).HasColumnName("name_ar").HasMaxLength(300).IsRequired();
        builder.Property(m => m.NameEn).HasColumnName("name_en").HasMaxLength(300).IsRequired();
        builder.Property(m => m.DescriptionAr).HasColumnName("description_ar");
        builder.Property(m => m.DescriptionEn).HasColumnName("description_en");
        builder.Property(m => m.LogoUrl).HasColumnName("logo_url");
        builder.Property(m => m.WebsiteUrl).HasColumnName("website_url");
        builder.Property(m => m.ContactEmail).HasColumnName("contact_email").HasMaxLength(254);
        builder.Property(m => m.ContactPhone).HasColumnName("contact_phone").HasMaxLength(20);
        builder.Property(m => m.AddressAr).HasColumnName("address_ar");
        builder.Property(m => m.AddressEn).HasColumnName("address_en");
        builder.Property(m => m.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(m => m.DepthLevel).HasColumnName("depth_level").HasDefaultValue(0);
        builder.Property(m => m.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.Property(m => m.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb");
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Ignore(m => m.DomainEvents);

        builder.HasMany(m => m.AuthConfigs)
            .WithOne()
            .HasForeignKey(c => c.MinistryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Endpoints)
            .WithOne()
            .HasForeignKey(e => e.MinistryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Webhooks)
            .WithOne()
            .HasForeignKey(w => w.MinistryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
