using MediatR;

namespace Sheba.ServiceRequest.Application.Commands.MarkPaymentComplete;

public sealed record MarkPaymentCompleteCommand(
    Guid PaymentOrderId,
    string? GatewayReference = null
) : IRequest<MarkPaymentCompleteResponse>;

public sealed record MarkPaymentCompleteResponse(Guid RequestId, string Message);
