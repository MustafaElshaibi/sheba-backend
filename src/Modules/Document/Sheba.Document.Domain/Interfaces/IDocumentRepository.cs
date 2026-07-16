namespace Sheba.Document.Domain.Interfaces;

public interface IDocumentRepository
{
    Task<Entities.Document?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Entities.Document>> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default);
    Task AddAsync(Entities.Document document, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
