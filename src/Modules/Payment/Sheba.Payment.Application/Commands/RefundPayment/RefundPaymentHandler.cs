using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Payment.Application.Common;
using Sheba.Payment.Domain.Entities;
using Sheba.Payment.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Payment.Application.Commands.RefundPayment;

public sealed class RefundPaymentHandler(
    IPaymentRepository repository,
    IPaymentGateway gateway,
    ILogger<RefundPaymentHandler> logger
) : IRequestHandler<RefundPaymentCommand, PaymentOrderDto>
{
    public async Task<PaymentOrderDto> Handle(RefundPaymentCommand command, CancellationToken ct)
    {
        var order = await repository.GetByIdAsync(command.PaymentOrderId, ct)
            ?? throw new NotFoundException("PaymentOrder", command.PaymentOrderId);

        // Let the aggregate's own guard (Completed-only) surface the domain error before we
        // spend a gateway call on an order that can't be refunded anyway.
        if (order.Status != Domain.Enums.PaymentStatus.Completed)
            throw new DomainException($"Cannot refund a payment order from status '{order.Status}'.");

        var result = await gateway.RefundAsync(
            order.Id, order.GatewayReference ?? string.Empty, order.TotalAmount, order.Currency, ct);

        await repository.AddTransactionAsync(
            PaymentTransaction.Create(order.Id, "Refund", order.TotalAmount, result.Success, result.RawResponse), ct);

        if (!result.Success)
        {
            await repository.SaveChangesAsync(ct);
            logger.LogWarning("[RefundPayment] Gateway declined refund for order {OrderId}", order.Id);
            throw new DomainException("Payment gateway declined the refund.");
        }

        order.Refund(result.RefundReference);
        await repository.SaveChangesAsync(ct);

        logger.LogInformation("[RefundPayment] Order {OrderId} refunded ({Amount} {Currency})",
            order.Id, order.TotalAmount, order.Currency);

        return PaymentOrderDto.FromEntity(order);
    }
}
