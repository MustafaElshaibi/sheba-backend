using Microsoft.EntityFrameworkCore;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;

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
    public DbSet<RelyingParty> RelyingParties => Set<RelyingParty>();
    public DbSet<RpRedirectUri> RpRedirectUris => Set<RpRedirectUri>();
    public DbSet<RpScope> RpScopes => Set<RpScope>();
    public DbSet<ScopeDefinition> ScopeDefinitions => Set<ScopeDefinition>();
    public DbSet<RefreshTokenFamily> RefreshTokenFamilies => Set<RefreshTokenFamily>();
    public DbSet<IdentityRequestDocument> IdentityRequestDocuments => Set<IdentityRequestDocument>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        // All tables live in the "identity" schema
        modelBuilder.HasDefaultSchema("identity");

        base.OnModelCreating(modelBuilder);
    }
}
