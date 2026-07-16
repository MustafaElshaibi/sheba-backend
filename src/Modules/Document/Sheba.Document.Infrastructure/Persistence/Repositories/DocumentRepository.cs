using Microsoft.EntityFrameworkCore;
using Sheba.Document.Domain.Interfaces;

namespace Sheba.Document.Infrastructure.Persistence.Repositories;

public sealed class DocumentRepository(DocumentDbContext db) : IDocumentRepository
{
    public async Task<Domain.Entities.Document?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Documents.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);

    public async Task<List<Domain.Entities.Document>> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default)
        => await db.Documents.Where(d => d.OwnerId == ownerId && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt).ToListAsync(ct);

    public async Task AddAsync(Domain.Entities.Document document, CancellationToken ct = default)
        => await db.Documents.AddAsync(document, ct);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
