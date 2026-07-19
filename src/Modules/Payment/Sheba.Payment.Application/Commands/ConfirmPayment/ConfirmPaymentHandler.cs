using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Payment.Application.Common;
using Sheba.Payment.Domain.Entities;
using Sheba.Payment.Domain.Enums;
using Sheba.Payment.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Payment.Application.Commands.ConfirmPayment;

public sealed class ConfirmPaymentHandler(
    IPaymentRepository repository,
    IPaymentGateway gateway,
    ILogger<ConfirmPaymentHandler> logger
) : IRequestHandler<ConfirmPaymentCommand, PaymentOrderDto>
{
    public async Task<PaymentOrderDto> Handle(ConfirmPaymentCommand command, CancellationToken ct)
    {
        var order = await repository.GetByIdAsync(command.PaymentOrderId, ct)
            ?? throw new NotFoundException("PaymentOrder", command.PaymentOrderId);

        // NotFoundException (not Forbidden) for a non-owner — anti-enumeration posture consistent
        // with the rest of the codebase: don't confirm another citizen's order exists.
        if (!command.IsAdmin && order.CitizenId != command.ActorId)
            throw new NotFoundException("PaymentOrder", command.PaymentOrderId);

        if (order.Status == PaymentStatus.Completed)
            return PaymentOrderDto.FromEntity(order);

        var result = await gateway.ChargeAsync(order.Id, order.TotalAmount, order.Currency, ct);

        await repository.AddTransactionAsync(
            PaymentTransaction.Create(order.Id, "Charge", order.TotalAmount, result.Success, result.RawResponse), ct);

        if (!result.Success)
        {
            order.MarkFailed();
            await repository.SaveChangesAsync(ct);
            logger.LogWarning("[ConfirmPayment] Gateway declined charge for order {OrderId}", order.Id);
            throw new DomainException("Payment gateway declined the charge.");
        }

        order.MarkPaid(result.GatewayReference);
        await repository.SaveChangesAsync(ct);

        logger.LogInformation("[ConfirmPayment] Order {OrderId} confirmed paid ({Amount} {Currency})",
            order.Id, order.TotalAmount, order.Currency);

        return PaymentOrderDto.FromEntity(order);
    }
}
