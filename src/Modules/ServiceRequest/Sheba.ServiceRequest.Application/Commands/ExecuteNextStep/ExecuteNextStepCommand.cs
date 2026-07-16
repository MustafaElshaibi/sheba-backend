using MediatR;

namespace Sheba.ServiceRequest.Application.Commands.ExecuteNextStep;

public sealed record ExecuteNextStepCommand(Guid RequestId) : IRequest<ExecuteNextStepResponse>;

public sealed record ExecuteNextStepResponse(
    bool Completed,
    int CurrentStep,
    string Status,
    string? PaymentUrl = null,
    string? Message = null);
