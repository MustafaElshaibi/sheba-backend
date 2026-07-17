using FluentAssertions;
using NSubstitute;
using Sheba.Identity.Application.Commands.CreateRefreshTokenFamily;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;

namespace Sheba.Identity.Tests.Application.Commands;

public sealed class CreateRefreshTokenFamilyHandlerTests
{
    private readonly IIdentityRepository _repo = Substitute.For<IIdentityRepository>();
    private readonly CreateRefreshTokenFamilyHandler _sut;

    public CreateRefreshTokenFamilyHandlerTests() => _sut = new CreateRefreshTokenFamilyHandler(_repo);

    [Fact]
    public async Task Handle_CreatesFamilyAtGenerationZero()
    {
        var subjectId = Guid.NewGuid();

        var result = await _sut.Handle(new CreateRefreshTokenFamilyCommand(subjectId, "sheba-portal"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Generation.Should().Be(0);
        result.Value.FamilyId.Should().NotBe(Guid.Empty);
        await _repo.Received(1).AddRefreshTokenFamilyAsync(
            Arg.Is<RefreshTokenFamily>(f => f.SubjectId == subjectId && f.ClientId == "sheba-portal"), default);
        await _repo.Received(1).SaveChangesAsync(default);
    }
}
