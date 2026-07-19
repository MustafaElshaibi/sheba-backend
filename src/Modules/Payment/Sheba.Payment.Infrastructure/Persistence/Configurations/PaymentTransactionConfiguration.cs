using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.Payment.Domain.Entities;

namespace Sheba.Payment.Infrastructure.Persistence.Configurations;

internal sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> b)
    {
        b.ToTable("payment_transactions");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(e => e.PaymentOrderId).HasColumnName("payment_order_id").IsRequired();
        b.HasIndex(e => e.PaymentOrderId);
        b.Property(e => e.TransactionType).HasColumnName("transaction_type").HasMaxLength(20).IsRequired();
        b.Property(e => e.Amount).HasColumnName("amount").HasColumnType("numeric(10,2)").IsRequired();
        b.Property(e => e.Succeeded).HasColumnName("succeeded").IsRequired();
        b.Property(e => e.GatewayResponse).HasColumnName("gateway_response");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Ignore(e => e.DomainEvents);
    }
}
