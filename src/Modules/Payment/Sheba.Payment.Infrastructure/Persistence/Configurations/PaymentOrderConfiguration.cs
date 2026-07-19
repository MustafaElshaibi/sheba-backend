using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.Payment.Domain.Entities;

namespace Sheba.Payment.Infrastructure.Persistence.Configurations;

internal sealed class PaymentOrderConfiguration : IEntityTypeConfiguration<PaymentOrder>
{
    public void Configure(EntityTypeBuilder<PaymentOrder> b)
    {
        b.ToTable("payment_orders");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(e => e.ServiceRequestId).HasColumnName("service_request_id").IsRequired();
        b.Property(e => e.CitizenId).HasColumnName("citizen_id").IsRequired();
        b.Property(e => e.OrderNumber).HasColumnName("order_number").HasMaxLength(30).IsRequired();
        b.HasIndex(e => e.OrderNumber).IsUnique();
        b.Property(e => e.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(10,2)").IsRequired();
        b.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(3).HasDefaultValue("YER");
        b.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(e => e.Description).HasColumnName("description");
        b.Property(e => e.PaymentUrl).HasColumnName("payment_url");
        b.Property(e => e.PaidAt).HasColumnName("paid_at");
        b.Property(e => e.GatewayReference).HasColumnName("gateway_reference");
        b.Property(e => e.RefundedAt).HasColumnName("refunded_at");
        b.Property(e => e.RefundReference).HasColumnName("refund_reference");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Ignore(e => e.DomainEvents);
    }
}
