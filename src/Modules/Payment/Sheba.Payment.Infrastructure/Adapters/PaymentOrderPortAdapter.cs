using Sheba.Payment.Domain.Entities;
using Sheba.Payment.Domain.Enums;
using Sheba.Payment.Domain.Interfaces;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Payment.Infrastructure.Adapters;

/// <summary>
/// Implements <see cref="IPaymentOrderPort"/> using <see cref="IPaymentRepository"/>. Lives in
/// Payment.Infrastructure (which owns the payment schema). Registered in PaymentModule so any
/// module that injects <see cref="IPaymentOrderPort"/> gets order data without touching
/// Payment.Domain/Payment.Infrastructure directly (T-ARC-1).
/// </summary>
public sealed class PaymentOrderPortAdapter(IPaymentRepository repository) : IPaymentOrderPort
{
    public async Task<PaymentOrderInfo?> GetByIdAsync(Guid paymentOrderId, CancellationToken ct = default)
    {
        var order = await repository.GetByIdAsync(paymentOrderId, ct);
        return order is null ? null : ToInfo(order);
    }

    public async Task<PaymentOrderInfo?> GetByServiceRequestIdAsync(Guid serviceRequestId, CancellationToken ct = default)
    {
        var order = await repository.GetByServiceRequestIdAsync(serviceRequestId, ct);
        return order is null ? null : ToInfo(order);
    }

    public async Task<PaymentOrderInfo> CreateOrderAsync(
        Guid serviceRequestId, Guid citizenId, decimal totalAmount, string currency, string? description,
        CancellationToken ct = default)
    {
        var order = PaymentOrder.Create(serviceRequestId, citizenId, totalAmount, currency, description);
        await repository.AddAsync(order, ct);
        await repository.SaveChangesAsync(ct);
        return ToInfo(order);
    }

    public async Task<PaymentOrderInfo> MarkPaidAsync(
        Guid paymentOrderId, string? gatewayReference, CancellationToken ct = default)
    {
        var order = await repository.GetByIdAsync(paymentOrderId, ct)
            ?? throw new InvalidOperationException($"PaymentOrder {paymentOrderId} not found.");

        if (order.Status != PaymentStatus.Completed)
        {
            order.MarkPaid(gatewayReference);
            await repository.SaveChangesAsync(ct);
        }

        return ToInfo(order);
    }

    private static PaymentOrderInfo ToInfo(PaymentOrder order) => new(
        order.Id, order.ServiceRequestId, order.CitizenId, order.OrderNumber,
        order.TotalAmount, order.Currency, (PaymentOrderStatus)(int)order.Status,
        order.PaymentUrl, order.GatewayReference);
}
