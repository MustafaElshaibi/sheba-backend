using MediatR;

namespace Sheba.ServiceRequest.Application.Commands.CancelServiceRequest;

/// <summary>Citizen cancels their own request (BR-SR-7: only before completion/rejection).</summary>
public sealed record CancelServiceRequestCommand(Guid RequestId, Guid CitizenId) : IRequest<CancelServiceRequestResponse>;

public sealed record CancelServiceRequestResponse(Guid RequestId, string Status, string Message);
