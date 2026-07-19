using MediatR;
using Sheba.Payment.Application.Common;

namespace Sheba.Payment.Application.Commands.ConfirmPayment;

/// <summary>
/// Confirms a citizen's own payment order via the (mock) gateway seam. ActorId/IsAdmin drive the
/// ownership check in the handler: a citizen may only confirm their own order. Replaces the old
/// ServiceRequest-owned <c>MarkPaymentCompleteCommand</c> — this module now owns the confirm step
/// and raises <c>PaymentCompletedEvent</c> instead of reaching into ServiceRequest directly.
/// </summary>
public sealed record ConfirmPaymentCommand(
    Guid PaymentOrderId,
    Guid ActorId,
    bool IsAdmin
) : IRequest<PaymentOrderDto>;
