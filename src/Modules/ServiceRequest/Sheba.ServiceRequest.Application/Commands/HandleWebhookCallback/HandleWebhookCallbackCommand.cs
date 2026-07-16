using MediatR;

namespace Sheba.ServiceRequest.Application.Commands.HandleWebhookCallback;

public sealed record HandleWebhookCallbackCommand(
    Guid MinistryId,
    string EventType,
    string PayloadJson,
    string? Signature = null
) : IRequest<HandleWebhookCallbackResponse>;

public sealed record HandleWebhookCallbackResponse(bool Accepted, string Message);
