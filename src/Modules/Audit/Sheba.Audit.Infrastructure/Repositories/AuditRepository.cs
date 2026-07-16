using Microsoft.EntityFrameworkCore;
using Sheba.Audit.Application.Interfaces;
using Sheba.Audit.Domain.Entities;
using Sheba.Audit.Infrastructure.Persistence;

namespace Sheba.Audit.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of IAuditRepository.
/// Append-only — no Update or Delete methods exposed.
/// </summary>
public sealed class AuditRepository(AuditDbContext db) : IAuditRepository
{
    public async Task AddAsync(AuditEvent auditEvent, CancellationToken ct)
    {
        await db.AuditEvents.AddAsync(auditEvent, ct);
    }

    public async Task<(List<AuditEvent> Items, int TotalCount)> GetPagedAsync(
        Guid? actorId,
        string? entityType,
        string? action,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var query = db.AuditEvents.AsNoTracking().AsQueryable();

        if (actorId.HasValue)
            query = query.Where(e => e.ActorId == actorId.Value);

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(e => e.EntityType == entityType);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(e => e.Action.Contains(action));

        if (from.HasValue)
            query = query.Where(e => e.Timestamp >= from.Value.ToDateTime(TimeOnly.MinValue));

        if (to.HasValue)
            query = query.Where(e => e.Timestamp <= to.Value.ToDateTime(TimeOnly.MaxValue));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
    }
}
