using FluentAssertions;
using NSubstitute;
using Sheba.Shared.Kernel.Exceptions;
using Sheba.Wallet.Application.Queries.GetRevocationStatus;
using Sheba.Wallet.Domain.Entities;
using Sheba.Wallet.Domain.Interfaces;

namespace Sheba.Wallet.Tests.Application;

public sealed class GetRevocationStatusHandlerTests
{
    private readonly IWalletRepository _repo = Substitute.For<IWalletRepository>();
    private GetRevocationStatusHandler Build() => new(_repo);

    [Fact]
    public async Task Handle_ActiveCredential_ReportsNotRevoked()
    {
        var credential = VerifiableCredential.Issue(
            Guid.NewGuid(), "DigitalIdentityCredential", "did:sheba:issuer", "did:sheba:citizen:x",
            "a.b.c", "{}", DateTime.UtcNow.AddDays(1));
        _repo.GetCredentialByIdAsync(credential.Id, Arg.Any<CancellationToken>()).Returns(credential);

        var result = await Build().Handle(new GetRevocationStatusQuery(credential.Id), default);

        result.IsRevoked.Should().BeFalse();
        result.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_RevokedCredential_ReportsRevokedWithTimestamp()
    {
        var credential = VerifiableCredential.Issue(
            Guid.NewGuid(), "DigitalIdentityCredential", "did:sheba:issuer", "did:sheba:citizen:x",
            "a.b.c", "{}", DateTime.UtcNow.AddDays(1));
        credential.Revoke();
        _repo.GetCredentialByIdAsync(credential.Id, Arg.Any<CancellationToken>()).Returns(credential);

        var result = await Build().Handle(new GetRevocationStatusQuery(credential.Id), default);

        result.IsRevoked.Should().BeTrue();
        result.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_NotFound_ThrowsNotFound()
    {
        var id = Guid.NewGuid();
        _repo.GetCredentialByIdAsync(id, Arg.Any<CancellationToken>()).Returns((VerifiableCredential?)null);

        var act = () => Build().Handle(new GetRevocationStatusQuery(id), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
