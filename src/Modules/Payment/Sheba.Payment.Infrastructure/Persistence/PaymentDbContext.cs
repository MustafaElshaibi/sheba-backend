using Microsoft.EntityFrameworkCore;
using Sheba.Payment.Domain.Entities;

namespace Sheba.Payment.Infrastructure.Persistence;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("payment");
        mb.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);
        base.OnModelCreating(mb);
    }
}
