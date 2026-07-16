using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Audit.Application.Interfaces;

namespace Sheba.Audit.Application.Queries.GetAuditLog;

/// <summary>
/// Handles GetAuditLogQuery — reads paged audit events with filters.
/// </summary>
public sealed class GetAuditLogHandler(
    IAuditRepository auditRepo,
    ILogger<GetAuditLogHandler> logger
) : IRequestHandler<GetAuditLogQuery, GetAuditLogResponse>
{
    public async Task<GetAuditLogResponse> Handle(GetAuditLogQuery request, CancellationToken ct)
    {
        var (items, totalCount) = await auditRepo.GetPagedAsync(
            request.ActorId,
            request.EntityType,
            request.Action,
            request.From,
            request.To,
            request.Page,
            request.PageSize,
            ct);

        var dtos = items.Select(e => new AuditEventDto(
            e.Id,
            e.ActorId,
            e.Action,
            e.EntityType,
            e.EntityId,
            e.Timestamp,
            e.IpAddress,
            e.Succeeded,
            e.ErrorMessage)).ToList();

        logger.LogDebug("[GetAuditLog] Returning {Count}/{Total} audit events (page {Page})",
            dtos.Count, totalCount, request.Page);

        return new GetAuditLogResponse(dtos, totalCount, request.Page, request.PageSize);
    }
}
