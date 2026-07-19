using MediatR;
using Sheba.Payment.Application.Commands.CreatePaymentOrder;
using Sheba.Payment.Domain.Interfaces;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Payment.Infrastructure.Adapters;

/// <summary>
/// Implements <see cref="IPaymentOrderPort"/> so any module that injects it gets order data
/// without touching Payment.Domain/Payment.Infrastructure directly (T-ARC-1). Reads go straight
/// to the repository; the write path (order creation) delegates to
/// <see cref="CreatePaymentOrderCommand"/> so the business rule (Payment.Application) has a
/// single home instead of being duplicated between this adapter and the command handler
/// (T-PAY-1). There is deliberately no MarkPaid/Confirm method on this port anymore — payment
/// confirmation is now a Payment-owned endpoint that raises <c>PaymentCompletedEvent</c>, and
/// ServiceRequest reacts to that event instead of calling back into Payment synchronously.
/// </summary>
public sealed class PaymentOrderPortAdapter(IPaymentRepository repository, IMediator mediator) : IPaymentOrderPort
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
        var dto = await mediator.Send(
            new CreatePaymentOrderCommand(serviceRequestId, citizenId, totalAmount, currency, description), ct);

        return new PaymentOrderInfo(
            dto.Id, dto.ServiceRequestId, dto.CitizenId, dto.OrderNumber,
            dto.TotalAmount, dto.Currency, Enum.Parse<PaymentOrderStatus>(dto.Status),
            dto.PaymentUrl, dto.GatewayReference);
    }

    private static PaymentOrderInfo ToInfo(Domain.Entities.PaymentOrder order) => new(
        order.Id, order.ServiceRequestId, order.CitizenId, order.OrderNumber,
        order.TotalAmount, order.Currency, (PaymentOrderStatus)(int)order.Status,
        order.PaymentUrl, order.GatewayReference);
}
