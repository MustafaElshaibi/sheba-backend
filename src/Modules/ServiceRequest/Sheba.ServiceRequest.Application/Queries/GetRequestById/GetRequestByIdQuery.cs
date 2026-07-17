using MediatR;
using Sheba.ServiceRequest.Domain.Enums;

namespace Sheba.ServiceRequest.Application.Queries.GetRequestById;

/// <summary>
/// A citizen may only fetch their own request; an admin may fetch any. The handler enforces
/// this — see the ownership check in GetRequestByIdHandler — rather than the endpoint, since
/// "own it or be an admin" needs the resource loaded first to compare.
/// </summary>
public sealed record GetRequestByIdQuery(Guid RequestId, Guid ActorId, bool IsAdmin) : IRequest<RequestDetailDto?>;

public sealed record RequestDetailDto(
    Guid Id, string ReferenceNumber, Guid ServiceId, Guid CitizenId,
    string Status, int CurrentStep, string Priority,
    DateTime SubmittedAt, DateTime? CompletedAt, DateTime? DueDate,
    string? RejectionReason, string? FormDataJson,
    List<StepExecutionDto> Steps);

public sealed record StepExecutionDto(
    Guid Id, int StepOrder, string Status, DateTime StartedAt,
    DateTime? CompletedAt, string? ActorType, string? ErrorMessage);
