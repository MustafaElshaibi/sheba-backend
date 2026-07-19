using Microsoft.EntityFrameworkCore;
using Sheba.Wallet.Domain.Entities;
using Sheba.Wallet.Domain.Interfaces;

namespace Sheba.Wallet.Infrastructure.Persistence.Repositories;

public sealed class WalletRepository(WalletDbContext db) : IWalletRepository
{
    public async Task<VerifiableCredential?> GetCredentialByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Credentials.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<VerifiableCredential?> GetCredentialByJwtAsync(string jwt, CancellationToken ct = default)
        => await db.Credentials.FirstOrDefaultAsync(c => c.Jwt == jwt, ct);

    public async Task<List<VerifiableCredential>> GetCredentialsBySubjectAsync(Guid subjectId, CancellationToken ct = default)
        => await db.Credentials.Where(c => c.SubjectId == subjectId)
            .OrderByDescending(c => c.IssuedAt).ToListAsync(ct);

    public async Task<bool> HasCredentialTypeAsync(Guid subjectId, string credentialType, CancellationToken ct = default)
        => await db.Credentials.AnyAsync(
            c => c.SubjectId == subjectId && c.CredentialType == credentialType && !c.IsRevoked, ct);

    public async Task AddCredentialAsync(VerifiableCredential credential, CancellationToken ct = default)
        => await db.Credentials.AddAsync(credential, ct);

    public async Task<DidDocument?> GetDidBySubjectAsync(Guid subjectId, CancellationToken ct = default)
        => await db.DidDocuments.FirstOrDefaultAsync(d => d.SubjectId == subjectId, ct);

    public async Task<DidDocument?> GetIssuerDidAsync(CancellationToken ct = default)
        => await db.DidDocuments.FirstOrDefaultAsync(d => d.SubjectId == null, ct);

    public async Task<DidDocument?> GetByDidAsync(string did, CancellationToken ct = default)
        => await db.DidDocuments.FirstOrDefaultAsync(d => d.Did == did, ct);

    public async Task AddDidAsync(DidDocument did, CancellationToken ct = default)
        => await db.DidDocuments.AddAsync(did, ct);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
