using MediatR;

namespace Sheba.ServiceRequest.Application.Commands.MarkPaymentComplete;

/// <summary>
/// Marks a citizen's own payment order paid (mock gateway callback shape — see T-PAY-1 for the
/// real gateway seam). ActorId/IsAdmin drive the ownership check in the handler: a citizen may
/// only confirm their own order.
/// </summary>
public sealed record MarkPaymentCompleteCommand(
    Guid PaymentOrderId,
    Guid ActorId,
    bool IsAdmin,
    string? GatewayReference = null
) : IRequest<MarkPaymentCompleteResponse>;

public sealed record MarkPaymentCompleteResponse(Guid RequestId, string Message);
