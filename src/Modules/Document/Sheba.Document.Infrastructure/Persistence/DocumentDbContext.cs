using Microsoft.EntityFrameworkCore;

namespace Sheba.Document.Infrastructure.Persistence;

public sealed class DocumentDbContext(DbContextOptions<DocumentDbContext> options) : DbContext(options)
{
    public DbSet<Domain.Entities.Document> Documents => Set<Domain.Entities.Document>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("document");
        mb.ApplyConfigurationsFromAssembly(typeof(DocumentDbContext).Assembly);
        base.OnModelCreating(mb);
    }
}
