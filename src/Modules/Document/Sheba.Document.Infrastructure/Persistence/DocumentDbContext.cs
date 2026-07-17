using Microsoft.EntityFrameworkCore;
using Sheba.Shared.Kernel.Outbox;

namespace Sheba.Document.Infrastructure.Persistence;

public sealed class DocumentDbContext(DbContextOptions<DocumentDbContext> options) : DbContext(options)
{
    public DbSet<Domain.Entities.Document> Documents => Set<Domain.Entities.Document>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("document");
        mb.ApplyConfigurationsFromAssembly(typeof(DocumentDbContext).Assembly);
        mb.ApplyConfiguration(new OutboxMessageConfiguration());
        mb.ApplyConfiguration(new InboxMessageConfiguration());
        base.OnModelCreating(mb);
    }
}
