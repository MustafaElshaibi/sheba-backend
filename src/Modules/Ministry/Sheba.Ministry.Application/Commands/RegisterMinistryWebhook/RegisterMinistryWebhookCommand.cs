using MediatR;

namespace Sheba.Ministry.Application.Commands.RegisterMinistryWebhook;

public sealed record RegisterMinistryWebhookCommand(
    Guid MinistryId,
    string EventType,
    string ShebaWebhookPath,
    string SigningSecret,      // plaintext — handler encrypts before storing
    Guid? EndpointId = null
) : IRequest<RegisterMinistryWebhookResponse>;

public sealed record RegisterMinistryWebhookResponse(Guid WebhookId, string Message);
