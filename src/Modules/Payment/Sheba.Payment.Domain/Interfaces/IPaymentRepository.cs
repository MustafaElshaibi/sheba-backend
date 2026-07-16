using Sheba.Payment.Domain.Entities;

namespace Sheba.Payment.Domain.Interfaces;

public interface IPaymentRepository
{
    Task<PaymentOrder?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PaymentOrder?> GetByServiceRequestIdAsync(Guid serviceRequestId, CancellationToken ct = default);
    Task AddAsync(PaymentOrder order, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
