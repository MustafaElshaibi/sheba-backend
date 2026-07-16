using MediatR;
using Sheba.ServiceRequest.Domain.Enums;

namespace Sheba.ServiceRequest.Application.Queries.GetMyRequests;

public sealed record GetMyRequestsQuery(Guid CitizenId) : IRequest<List<CitizenRequestDto>>;

public sealed record CitizenRequestDto(
    Guid Id, string ReferenceNumber, Guid ServiceId,
    string Status, int CurrentStep, string Priority,
    DateTime SubmittedAt, DateTime? CompletedAt, DateTime? DueDate);
