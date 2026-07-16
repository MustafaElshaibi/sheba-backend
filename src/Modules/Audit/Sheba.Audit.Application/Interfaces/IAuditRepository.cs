using Sheba.Audit.Domain.Entities;

namespace Sheba.Audit.Application.Interfaces;

/// <summary>
/// Repository for audit events. Write-heavy, read via admin query only.
/// </summary>
public interface IAuditRepository
{
    /// <summary>Appends a new audit event (INSERT only — no update/delete).</summary>
    Task AddAsync(AuditEvent auditEvent, CancellationToken ct = default);

    /// <summary>Queries audit events with filtering and pagination.</summary>
    Task<(List<AuditEvent> Items, int TotalCount)> GetPagedAsync(
        Guid? actorId,
        string? entityType,
        string? action,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
