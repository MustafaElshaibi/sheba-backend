using Microsoft.EntityFrameworkCore;
using Sheba.Payment.Domain.Entities;
using Sheba.Shared.Kernel.Outbox;

namespace Sheba.Payment.Infrastructure.Persistence;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("payment");
        mb.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);
        mb.ApplyConfiguration(new OutboxMessageConfiguration());
        mb.ApplyConfiguration(new InboxMessageConfiguration());
        base.OnModelCreating(mb);
    }
}
