using Microsoft.EntityFrameworkCore;
using Sheba.Ministry.Domain.Entities;
namespace Sheba.Ministry.Infrastructure.Persistence;
public class MinistryDbContext : DbContext
{
    public MinistryDbContext(DbContextOptions<MinistryDbContext> options) : base(options) { }
    public DbSet<Ministry.Domain.Entities.Ministry> Ministries => Set<Ministry.Domain.Entities.Ministry>();
    public DbSet<MinistryAuthConfig> AuthConfigs => Set<MinistryAuthConfig>();
    public DbSet<MinistryAuthCredential> AuthCredentials => Set<MinistryAuthCredential>();
    public DbSet<MinistryEndpoint> Endpoints => Set<MinistryEndpoint>();
    public DbSet<MinistryWebhook> Webhooks => Set<MinistryWebhook>();
    protected override void OnModelCreating(ModelBuilder mb)
    { mb.HasDefaultSchema("ministry"); mb.ApplyConfigurationsFromAssembly(typeof(MinistryDbContext).Assembly); base.OnModelCreating(mb); }
}
