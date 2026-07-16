using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.ServiceRequest.Domain.Entities;

namespace Sheba.ServiceRequest.Infrastructure.Persistence.Configurations;

internal sealed class ServiceFeeConfiguration : IEntityTypeConfiguration<ServiceFee>
{
    public void Configure(EntityTypeBuilder<ServiceFee> b)
    {
        b.ToTable("service_fees");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(e => e.ServiceId).HasColumnName("service_id").IsRequired();
        b.Property(e => e.FeeType).HasColumnName("fee_type").HasMaxLength(50).IsRequired();
        b.Property(e => e.NameAr).HasColumnName("name_ar").HasMaxLength(200).IsRequired();
        b.Property(e => e.NameEn).HasColumnName("name_en").HasMaxLength(200).IsRequired();
        b.Property(e => e.Amount).HasColumnName("amount").HasColumnType("numeric(10,2)").IsRequired();
        b.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(3).HasDefaultValue("YER");
        b.Property(e => e.IsMandatory).HasColumnName("is_mandatory").HasDefaultValue(true);
        b.Property(e => e.ValidFrom).HasColumnName("valid_from").IsRequired();
        b.Property(e => e.ValidUntil).HasColumnName("valid_until");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Ignore(e => e.DomainEvents);
    }
}
