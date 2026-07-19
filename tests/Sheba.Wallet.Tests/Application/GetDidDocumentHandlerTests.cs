using FluentAssertions;
using NSubstitute;
using Sheba.Shared.Kernel.Exceptions;
using Sheba.Wallet.Application.Queries.GetDidDocument;
using Sheba.Wallet.Domain.Entities;
using Sheba.Wallet.Domain.Interfaces;

namespace Sheba.Wallet.Tests.Application;

public sealed class GetDidDocumentHandlerTests
{
    private readonly IWalletRepository _repo = Substitute.For<IWalletRepository>();
    private GetDidDocumentHandler Build() => new(_repo);

    [Fact]
    public async Task Handle_KnownDid_ReturnsDocument()
    {
        var did = DidDocument.Create("did:sheba:issuer", "-----BEGIN PUBLIC KEY-----...");
        _repo.GetByDidAsync("did:sheba:issuer", Arg.Any<CancellationToken>()).Returns(did);

        var result = await Build().Handle(new GetDidDocumentQuery("did:sheba:issuer"), default);

        result.Did.Should().Be("did:sheba:issuer");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UnknownDid_ThrowsNotFound()
    {
        _repo.GetByDidAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((DidDocument?)null);

        var act = () => Build().Handle(new GetDidDocumentQuery("did:sheba:citizen:missing"), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
