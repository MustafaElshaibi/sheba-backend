using Microsoft.EntityFrameworkCore;
using Sheba.Shared.Kernel.Outbox;
using Sheba.Wallet.Domain.Entities;

namespace Sheba.Wallet.Infrastructure.Persistence;

public sealed class WalletDbContext(DbContextOptions<WalletDbContext> options) : DbContext(options)
{
    public DbSet<VerifiableCredential> Credentials => Set<VerifiableCredential>();
    public DbSet<DidDocument> DidDocuments => Set<DidDocument>();
    public DbSet<CredentialSchema> CredentialSchemas => Set<CredentialSchema>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("wallet");
        mb.ApplyConfigurationsFromAssembly(typeof(WalletDbContext).Assembly);
        mb.ApplyConfiguration(new OutboxMessageConfiguration());
        mb.ApplyConfiguration(new InboxMessageConfiguration());
        base.OnModelCreating(mb);
    }
}
