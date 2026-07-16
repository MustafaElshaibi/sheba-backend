using MediatR;

namespace Sheba.Audit.Application.Queries.GetAuditLog;

/// <summary>
/// Admin query: retrieves paged audit log with optional filters.
/// </summary>
public sealed record GetAuditLogQuery(
    Guid? ActorId = null,
    string? EntityType = null,
    string? Action = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 25
) : IRequest<GetAuditLogResponse>;

public sealed record AuditEventDto(
    Guid Id,
    Guid ActorId,
    string Action,
    string? EntityType,
    Guid? EntityId,
    DateTime Timestamp,
    string? IpAddress,
    bool Succeeded,
    string? ErrorMessage);

public sealed record GetAuditLogResponse(
    List<AuditEventDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
