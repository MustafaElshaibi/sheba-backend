using Microsoft.EntityFrameworkCore;
using Sheba.Wallet.Domain.Entities;

namespace Sheba.Wallet.Infrastructure.Persistence;

public sealed class WalletDbContext(DbContextOptions<WalletDbContext> options) : DbContext(options)
{
    public DbSet<VerifiableCredential> Credentials => Set<VerifiableCredential>();
    public DbSet<DidDocument> DidDocuments => Set<DidDocument>();
    public DbSet<CredentialSchema> CredentialSchemas => Set<CredentialSchema>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("wallet");
        mb.ApplyConfigurationsFromAssembly(typeof(WalletDbContext).Assembly);
        base.OnModelCreating(mb);
    }
}
