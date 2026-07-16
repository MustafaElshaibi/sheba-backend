using MediatR;
using Sheba.ServiceRequest.Domain.Enums;

namespace Sheba.ServiceRequest.Application.Queries.GetRequestById;

public sealed record GetRequestByIdQuery(Guid RequestId) : IRequest<RequestDetailDto?>;

public sealed record RequestDetailDto(
    Guid Id, string ReferenceNumber, Guid ServiceId, Guid CitizenId,
    string Status, int CurrentStep, string Priority,
    DateTime SubmittedAt, DateTime? CompletedAt, DateTime? DueDate,
    string? RejectionReason, string? FormDataJson,
    List<StepExecutionDto> Steps);

public sealed record StepExecutionDto(
    Guid Id, int StepOrder, string Status, DateTime StartedAt,
    DateTime? CompletedAt, string? ActorType, string? ErrorMessage);
