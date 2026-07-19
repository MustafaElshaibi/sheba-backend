using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Wallet.Application.Queries.VerifyCredential;
using Sheba.Wallet.Domain.Entities;
using Sheba.Wallet.Domain.Interfaces;

namespace Sheba.Wallet.Tests.Application;

public sealed class VerifyCredentialHandlerTests
{
    private readonly IWalletRepository _repo = Substitute.For<IWalletRepository>();
    private readonly ICredentialSigner _signer = Substitute.For<ICredentialSigner>();

    private VerifyCredentialHandler Build() =>
        new(_repo, _signer, NullLogger<VerifyCredentialHandler>.Instance);

    private const string FakeJwt = "aaa.bbb.ccc";

    private static VerifiableCredential ValidCredential() =>
        VerifiableCredential.Issue(
            Guid.NewGuid(), "DigitalIdentityCredential", "did:sheba:issuer", "did:sheba:citizen:x",
            FakeJwt, "{\"name\":\"Ahmed\"}", DateTime.UtcNow.AddDays(1));

    [Fact]
    public async Task Handle_MalformedJwt_ReturnsInvalid_NeverCallsSigner()
    {
        var sut = Build();

        var result = await sut.Handle(new VerifyCredentialQuery("not-a-jwt"), default);

        result.IsValid.Should().BeFalse();
        _signer.DidNotReceive().VerifyIssuerSignature(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_BadSignature_ReturnsInvalid_NeverQueriesRepository()
    {
        _signer.VerifyIssuerSignature(FakeJwt).Returns(false);
        var sut = Build();

        var result = await sut.Handle(new VerifyCredentialQuery(FakeJwt), default);

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Contain("Signature");
        await _repo.DidNotReceive().GetCredentialByJwtAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidSignature_NotInDb_ReturnsInvalid_NotRecognized()
    {
        _signer.VerifyIssuerSignature(FakeJwt).Returns(true);
        _repo.GetCredentialByJwtAsync(FakeJwt, Arg.Any<CancellationToken>()).Returns((VerifiableCredential?)null);
        var sut = Build();

        var result = await sut.Handle(new VerifyCredentialQuery(FakeJwt), default);

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Contain("not recognized");
    }

    [Fact]
    public async Task Handle_Revoked_ReturnsInvalid()
    {
        var credential = ValidCredential();
        credential.Revoke();
        _signer.VerifyIssuerSignature(FakeJwt).Returns(true);
        _repo.GetCredentialByJwtAsync(FakeJwt, Arg.Any<CancellationToken>()).Returns(credential);
        var sut = Build();

        var result = await sut.Handle(new VerifyCredentialQuery(FakeJwt), default);

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Contain("revoked");
        result.CredentialId.Should().Be(credential.Id);
    }

    [Fact]
    public async Task Handle_Expired_ReturnsInvalid()
    {
        var credential = VerifiableCredential.Issue(
            Guid.NewGuid(), "DigitalIdentityCredential", "did:sheba:issuer", "did:sheba:citizen:x",
            FakeJwt, "{}", DateTime.UtcNow.AddDays(-1)); // already expired
        _signer.VerifyIssuerSignature(FakeJwt).Returns(true);
        _repo.GetCredentialByJwtAsync(FakeJwt, Arg.Any<CancellationToken>()).Returns(credential);
        var sut = Build();

        var result = await sut.Handle(new VerifyCredentialQuery(FakeJwt), default);

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Contain("expired");
    }

    [Fact]
    public async Task Handle_ValidUnrevokedUnexpired_ReturnsValidWithClaims()
    {
        var credential = ValidCredential();
        _signer.VerifyIssuerSignature(FakeJwt).Returns(true);
        _repo.GetCredentialByJwtAsync(FakeJwt, Arg.Any<CancellationToken>()).Returns(credential);
        var sut = Build();

        var result = await sut.Handle(new VerifyCredentialQuery(FakeJwt), default);

        result.IsValid.Should().BeTrue();
        result.Reason.Should().BeNull();
        result.CredentialId.Should().Be(credential.Id);
        result.Claims.Should().NotBeNull().And.ContainKey("name");
    }
}
