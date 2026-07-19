using Sheba.Wallet.Domain.Entities;

namespace Sheba.Wallet.Domain.Interfaces;

public interface IWalletRepository
{
    Task<VerifiableCredential?> GetCredentialByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Exact-match lookup by the stored JWT — used by the verify/presentation flow
    /// (T-WAL-2) to confirm a presented VC was actually issued by us (and to read its current
    /// revocation status), without trusting any claim baked into the caller-supplied token.</summary>
    Task<VerifiableCredential?> GetCredentialByJwtAsync(string jwt, CancellationToken ct = default);
    Task<List<VerifiableCredential>> GetCredentialsBySubjectAsync(Guid subjectId, CancellationToken ct = default);
    Task<bool> HasCredentialTypeAsync(Guid subjectId, string credentialType, CancellationToken ct = default);
    Task AddCredentialAsync(VerifiableCredential credential, CancellationToken ct = default);

    Task<DidDocument?> GetDidBySubjectAsync(Guid subjectId, CancellationToken ct = default);
    Task<DidDocument?> GetIssuerDidAsync(CancellationToken ct = default);
    /// <summary>Resolves any DID document (issuer or subject) by its DID string — the public
    /// resolution endpoint (T-WAL-2, BR-WA-2) doesn't know which kind it's given.</summary>
    Task<DidDocument?> GetByDidAsync(string did, CancellationToken ct = default);
    Task AddDidAsync(DidDocument did, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
