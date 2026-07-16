using Microsoft.EntityFrameworkCore;
using Sheba.Ministry.Domain.Entities;
using Sheba.Ministry.Domain.Interfaces;

namespace Sheba.Ministry.Infrastructure.Persistence.Repositories;

public sealed class MinistryRepository(MinistryDbContext db) : IMinistryRepository
{
    // ── Ministry ──────────────────────────────────────────────────────────
    public async Task<Domain.Entities.Ministry?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Ministries
            .Include(m => m.AuthConfigs)
            .Include(m => m.Endpoints)
            .Include(m => m.Webhooks)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<Domain.Entities.Ministry?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await db.Ministries.FirstOrDefaultAsync(m => m.Code == code, ct);

    public async Task<List<Domain.Entities.Ministry>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var query = db.Ministries.AsQueryable();
        if (!includeInactive)
            query = query.Where(m => m.IsActive);
        return await query.OrderBy(m => m.DisplayOrder).ThenBy(m => m.NameEn).ToListAsync(ct);
    }

    public async Task<List<Domain.Entities.Ministry>> GetSubMinistriesAsync(Guid parentId, CancellationToken ct = default)
        => await db.Ministries
            .Where(m => m.ParentMinistryId == parentId)
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync(ct);

    public async Task AddAsync(Domain.Entities.Ministry ministry, CancellationToken ct = default)
        => await db.Ministries.AddAsync(ministry, ct);

    // ── AuthConfig ────────────────────────────────────────────────────────
    public async Task<MinistryAuthConfig?> GetAuthConfigByIdAsync(Guid id, CancellationToken ct = default)
        => await db.AuthConfigs
            .Include(c => c.Credential)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<List<MinistryAuthConfig>> GetAuthConfigsByMinistryAsync(Guid ministryId, CancellationToken ct = default)
        => await db.AuthConfigs
            .Include(c => c.Credential)
            .Where(c => c.MinistryId == ministryId)
            .ToListAsync(ct);

    public async Task AddAuthConfigAsync(MinistryAuthConfig config, CancellationToken ct = default)
        => await db.AuthConfigs.AddAsync(config, ct);

    // ── AuthCredential ────────────────────────────────────────────────────
    public async Task<MinistryAuthCredential?> GetCredentialByAuthConfigIdAsync(Guid authConfigId, CancellationToken ct = default)
        => await db.AuthCredentials.FirstOrDefaultAsync(c => c.AuthConfigId == authConfigId, ct);

    public async Task AddCredentialAsync(MinistryAuthCredential credential, CancellationToken ct = default)
        => await db.AuthCredentials.AddAsync(credential, ct);

    public void RemoveCredential(MinistryAuthCredential credential)
        => db.AuthCredentials.Remove(credential);

    // ── Endpoint ──────────────────────────────────────────────────────────
    public async Task<MinistryEndpoint?> GetEndpointByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Endpoints.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<List<MinistryEndpoint>> GetEndpointsByMinistryAsync(Guid ministryId, CancellationToken ct = default)
        => await db.Endpoints.Where(e => e.MinistryId == ministryId).OrderBy(e => e.Code).ToListAsync(ct);

    public async Task AddEndpointAsync(MinistryEndpoint endpoint, CancellationToken ct = default)
        => await db.Endpoints.AddAsync(endpoint, ct);

    // ── Webhook ───────────────────────────────────────────────────────────
    public async Task<MinistryWebhook?> GetWebhookByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Webhooks.FirstOrDefaultAsync(w => w.Id == id, ct);

    public async Task<List<MinistryWebhook>> GetWebhooksByMinistryAsync(Guid ministryId, CancellationToken ct = default)
        => await db.Webhooks.Where(w => w.MinistryId == ministryId).ToListAsync(ct);

    public async Task AddWebhookAsync(MinistryWebhook webhook, CancellationToken ct = default)
        => await db.Webhooks.AddAsync(webhook, ct);

    // ── Unit of Work ──────────────────────────────────────────────────────
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
