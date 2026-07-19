using Microsoft.EntityFrameworkCore;
using Sheba.Payment.Domain.Entities;
using Sheba.Payment.Domain.Interfaces;

namespace Sheba.Payment.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository(PaymentDbContext db) : IPaymentRepository
{
    public async Task<PaymentOrder?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.PaymentOrders.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<PaymentOrder?> GetByServiceRequestIdAsync(Guid serviceRequestId, CancellationToken ct = default)
        => await db.PaymentOrders.FirstOrDefaultAsync(o => o.ServiceRequestId == serviceRequestId, ct);

    public async Task AddAsync(PaymentOrder order, CancellationToken ct = default)
        => await db.PaymentOrders.AddAsync(order, ct);

    public async Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken ct = default)
        => await db.PaymentTransactions.AddAsync(transaction, ct);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
