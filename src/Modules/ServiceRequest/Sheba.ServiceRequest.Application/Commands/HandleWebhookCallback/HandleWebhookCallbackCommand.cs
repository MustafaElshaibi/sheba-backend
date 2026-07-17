using MediatR;

namespace Sheba.ServiceRequest.Application.Commands.HandleWebhookCallback;

/// <summary>
/// A raw inbound ministry webhook. <see cref="PayloadJson"/> is the exact raw request body — it
/// must be the bytes the signature was computed over, so it is never re-serialized before
/// verification. The signature/timestamp/delivery-id come from the X-Sheba-* headers (§7.4).
/// </summary>
public sealed record HandleWebhookCallbackCommand(
    Guid MinistryId,
    string EventType,
    string PayloadJson,
    string? Signature = null,
    string? Timestamp = null,
    string? DeliveryId = null
) : IRequest<HandleWebhookCallbackResponse>;

public sealed record HandleWebhookCallbackResponse(bool Accepted, string Message);
