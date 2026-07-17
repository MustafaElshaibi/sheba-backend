using Microsoft.EntityFrameworkCore;
using Sheba.Ministry.Domain.Entities;
using Sheba.Shared.Kernel.Outbox;
namespace Sheba.Ministry.Infrastructure.Persistence;
public class MinistryDbContext : DbContext
{
    public MinistryDbContext(DbContextOptions<MinistryDbContext> options) : base(options) { }
    public DbSet<Ministry.Domain.Entities.Ministry> Ministries => Set<Ministry.Domain.Entities.Ministry>();
    public DbSet<MinistryAuthConfig> AuthConfigs => Set<MinistryAuthConfig>();
    public DbSet<MinistryAuthCredential> AuthCredentials => Set<MinistryAuthCredential>();
    public DbSet<MinistryEndpoint> Endpoints => Set<MinistryEndpoint>();
    public DbSet<MinistryWebhook> Webhooks => Set<MinistryWebhook>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("ministry");
        mb.ApplyConfigurationsFromAssembly(typeof(MinistryDbContext).Assembly);
        mb.ApplyConfiguration(new OutboxMessageConfiguration());
        mb.ApplyConfiguration(new InboxMessageConfiguration());
        base.OnModelCreating(mb);
    }
}
