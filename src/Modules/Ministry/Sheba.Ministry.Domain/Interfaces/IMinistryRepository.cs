using Sheba.Ministry.Domain.Entities;

namespace Sheba.Ministry.Domain.Interfaces;

/// <summary>
/// Application-layer repository abstraction for the Ministry module.
/// Implemented by EF Core in Sheba.Ministry.Infrastructure.
/// </summary>
public interface IMinistryRepository
{
    // ── Ministry ──────────────────────────────────────────────────────────
    Task<Entities.Ministry?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Entities.Ministry?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<List<Entities.Ministry>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<List<Entities.Ministry>> GetSubMinistriesAsync(Guid parentId, CancellationToken ct = default);
    Task AddAsync(Entities.Ministry ministry, CancellationToken ct = default);

    // ── AuthConfig ────────────────────────────────────────────────────────
    Task<MinistryAuthConfig?> GetAuthConfigByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<MinistryAuthConfig>> GetAuthConfigsByMinistryAsync(Guid ministryId, CancellationToken ct = default);
    /// <summary>All active auth configs across every ministry — feeds the health sweep job.</summary>
    Task<List<MinistryAuthConfig>> GetAllActiveAuthConfigsAsync(CancellationToken ct = default);
    Task AddAuthConfigAsync(MinistryAuthConfig config, CancellationToken ct = default);

    // ── AuthCredential ────────────────────────────────────────────────────
    Task<MinistryAuthCredential?> GetCredentialByAuthConfigIdAsync(Guid authConfigId, CancellationToken ct = default);
    Task AddCredentialAsync(MinistryAuthCredential credential, CancellationToken ct = default);
    void RemoveCredential(MinistryAuthCredential credential);

    // ── Endpoint ──────────────────────────────────────────────────────────
    Task<MinistryEndpoint?> GetEndpointByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<MinistryEndpoint>> GetEndpointsByMinistryAsync(Guid ministryId, CancellationToken ct = default);
    Task AddEndpointAsync(MinistryEndpoint endpoint, CancellationToken ct = default);

    // ── Webhook ───────────────────────────────────────────────────────────
    Task<MinistryWebhook?> GetWebhookByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<MinistryWebhook>> GetWebhooksByMinistryAsync(Guid ministryId, CancellationToken ct = default);
    Task AddWebhookAsync(MinistryWebhook webhook, CancellationToken ct = default);

    // ── Unit of Work ──────────────────────────────────────────────────────
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
