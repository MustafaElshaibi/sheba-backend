using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.ServiceRequest.Domain.Entities;

namespace Sheba.ServiceRequest.Infrastructure.Persistence.Configurations;

internal sealed class ServiceCategoryConfiguration : IEntityTypeConfiguration<ServiceCategory>
{
    public void Configure(EntityTypeBuilder<ServiceCategory> b)
    {
        b.ToTable("service_categories");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(e => e.ParentId).HasColumnName("parent_id");
        b.Property(e => e.NameAr).HasColumnName("name_ar").HasMaxLength(200).IsRequired();
        b.Property(e => e.NameEn).HasColumnName("name_en").HasMaxLength(200).IsRequired();
        b.Property(e => e.IconUrl).HasColumnName("icon_url");
        b.Property(e => e.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        b.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Ignore(e => e.DomainEvents);

        b.HasMany(e => e.Services).WithOne().HasForeignKey(s => s.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
