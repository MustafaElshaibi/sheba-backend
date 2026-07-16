using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Ministry.Domain.Entities;
using Sheba.Ministry.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Ministry.Application.Commands.RegisterMinistryWebhook;

public sealed class RegisterMinistryWebhookHandler(
    IMinistryRepository repository,
    ICredentialEncryptor encryptor,
    ILogger<RegisterMinistryWebhookHandler> logger
) : IRequestHandler<RegisterMinistryWebhookCommand, RegisterMinistryWebhookResponse>
{
    public async Task<RegisterMinistryWebhookResponse> Handle(
        RegisterMinistryWebhookCommand request, CancellationToken ct)
    {
        _ = await repository.GetByIdAsync(request.MinistryId, ct)
            ?? throw new NotFoundException("Ministry", request.MinistryId);

        var webhook = MinistryWebhook.Create(
            request.MinistryId,
            request.EventType,
            request.ShebaWebhookPath,
            encryptor.Encrypt(request.SigningSecret),
            request.EndpointId);

        await repository.AddWebhookAsync(webhook, ct);
        await repository.SaveChangesAsync(ct);

        logger.LogInformation("[RegisterWebhook] Created webhook {EventType} for Ministry {Id}",
            request.EventType, request.MinistryId);

        return new RegisterMinistryWebhookResponse(webhook.Id, "Webhook registered.");
    }
}
