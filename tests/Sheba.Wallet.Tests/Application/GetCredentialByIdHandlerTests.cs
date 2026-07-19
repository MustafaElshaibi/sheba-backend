using FluentAssertions;
using NSubstitute;
using Sheba.Shared.Kernel.Exceptions;
using Sheba.Wallet.Application.Queries.GetCredentialById;
using Sheba.Wallet.Domain.Entities;
using Sheba.Wallet.Domain.Interfaces;

namespace Sheba.Wallet.Tests.Application;

public sealed class GetCredentialByIdHandlerTests
{
    private readonly IWalletRepository _repo = Substitute.For<IWalletRepository>();
    private GetCredentialByIdHandler Build() => new(_repo);

    private static VerifiableCredential CredentialFor(Guid subjectId) =>
        VerifiableCredential.Issue(
            subjectId, "DigitalIdentityCredential", "did:sheba:issuer", "did:sheba:citizen:x",
            "a.b.c", "{}", DateTime.UtcNow.AddDays(1));

    [Fact]
    public async Task Handle_Owner_ReturnsCredential()
    {
        var subjectId = Guid.NewGuid();
        var credential = CredentialFor(subjectId);
        _repo.GetCredentialByIdAsync(credential.Id, Arg.Any<CancellationToken>()).Returns(credential);

        var result = await Build().Handle(new GetCredentialByIdQuery(credential.Id, subjectId, IsAdmin: false), default);

        result.Id.Should().Be(credential.Id);
    }

    [Fact]
    public async Task Handle_NonOwner_ThrowsNotFound()
    {
        var credential = CredentialFor(Guid.NewGuid());
        _repo.GetCredentialByIdAsync(credential.Id, Arg.Any<CancellationToken>()).Returns(credential);

        var act = () => Build().Handle(new GetCredentialByIdQuery(credential.Id, Guid.NewGuid(), IsAdmin: false), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NotFound_ThrowsNotFound()
    {
        var id = Guid.NewGuid();
        _repo.GetCredentialByIdAsync(id, Arg.Any<CancellationToken>()).Returns((VerifiableCredential?)null);

        var act = () => Build().Handle(new GetCredentialByIdQuery(id, Guid.NewGuid(), IsAdmin: false), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
