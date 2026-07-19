using Sheba.Payment.Domain.Entities;

namespace Sheba.Payment.Application.Common;

public sealed record PaymentOrderDto(
    Guid Id,
    Guid ServiceRequestId,
    Guid CitizenId,
    string OrderNumber,
    decimal TotalAmount,
    string Currency,
    string Status,
    string? Description,
    string? PaymentUrl,
    string? GatewayReference,
    DateTime? PaidAt,
    DateTime? RefundedAt,
    string? RefundReference)
{
    public static PaymentOrderDto FromEntity(PaymentOrder order) => new(
        order.Id, order.ServiceRequestId, order.CitizenId, order.OrderNumber,
        order.TotalAmount, order.Currency, order.Status.ToString(), order.Description,
        order.PaymentUrl, order.GatewayReference, order.PaidAt, order.RefundedAt, order.RefundReference);
}
