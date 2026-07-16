using Sheba.Wallet.Domain.Entities;

namespace Sheba.Wallet.Domain.Interfaces;

public interface IWalletRepository
{
    Task<VerifiableCredential?> GetCredentialByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<VerifiableCredential>> GetCredentialsBySubjectAsync(Guid subjectId, CancellationToken ct = default);
    Task<bool> HasCredentialTypeAsync(Guid subjectId, string credentialType, CancellationToken ct = default);
    Task AddCredentialAsync(VerifiableCredential credential, CancellationToken ct = default);

    Task<DidDocument?> GetDidBySubjectAsync(Guid subjectId, CancellationToken ct = default);
    Task<DidDocument?> GetIssuerDidAsync(CancellationToken ct = default);
    Task AddDidAsync(DidDocument did, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
