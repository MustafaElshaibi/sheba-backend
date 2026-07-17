using Microsoft.EntityFrameworkCore;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Outbox;

namespace Sheba.Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Identity module.
/// Mapped exclusively to the "identity" PostgreSQL schema.
/// OpenIddict entities are also registered here via UseEntityFrameworkCore().
///
/// Architecture rule: No other module may reference or inject this DbContext.
/// Cross-module data needs go via IDomainEvent or ICitizenQueryService (in SharedKernel).
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<IdentityRequest> IdentityRequests => Set<IdentityRequest>();
    public DbSet<OtpRecord> OtpRecords => Set<OtpRecord>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AdminRecoveryCode> AdminRecoveryCodes => Set<AdminRecoveryCode>();
    public DbSet<RelyingParty> RelyingParties => Set<RelyingParty>();
    public DbSet<RpRedirectUri> RpRedirectUris => Set<RpRedirectUri>();
    public DbSet<RpScope> RpScopes => Set<RpScope>();
    public DbSet<ScopeDefinition> ScopeDefinitions => Set<ScopeDefinition>();
    public DbSet<RefreshTokenFamily> RefreshTokenFamilies => Set<RefreshTokenFamily>();
    public DbSet<IdentityRequestDocument> IdentityRequestDocuments => Set<IdentityRequestDocument>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        // Shared outbox/inbox primitives (T-EVT-1) live in Shared.Kernel, outside this assembly,
        // so their configurations must be applied explicitly rather than assembly-scanned.
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());

        // All tables live in the "identity" schema
        modelBuilder.HasDefaultSchema("identity");

        base.OnModelCreating(modelBuilder);
    }
}
