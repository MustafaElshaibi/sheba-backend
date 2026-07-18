using MediatR;

namespace Sheba.ServiceRequest.Application.Commands.SubmitServiceRequest;

public sealed record SubmitServiceRequestCommand(
    Guid ServiceId,
    Guid CitizenId,
    string FormDataJson,
    int CitizenLoa,
    string Priority = "NORMAL"
) : IRequest<SubmitServiceRequestResponse>;

public sealed record SubmitServiceRequestResponse(
    Guid RequestId,
    string ReferenceNumber,
    string Status,
    string Message);
