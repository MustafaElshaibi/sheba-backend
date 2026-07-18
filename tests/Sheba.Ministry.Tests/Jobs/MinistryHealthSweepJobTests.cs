using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Ministry.Application.Commands.TestMinistryConnection;
using Sheba.Ministry.Domain.Entities;
using Sheba.Ministry.Domain.Enums;
using Sheba.Ministry.Domain.Interfaces;
using Sheba.Ministry.Infrastructure.Jobs;

namespace Sheba.Ministry.Tests.Jobs;

public sealed class MinistryHealthSweepJobTests
{
    private static MinistryAuthConfig BuildConfig() =>
        MinistryAuthConfig.Create(Guid.NewGuid(), "Cfg", MinistryAuthType.ApiKey, "https://ministry.example.gov");

    private static MinistryHealthSweepJob Build(
        IMinistryRepository repository, IMediator mediator) =>
        new(repository, mediator, NullLogger<MinistryHealthSweepJob>.Instance);

    [Fact]
    public async Task SweepAsync_SendsTestConnectionCommand_ForEveryActiveConfig()
    {
        var configs = new List<MinistryAuthConfig> { BuildConfig(), BuildConfig(), BuildConfig() };
        var repository = Substitute.For<IMinistryRepository>();
        repository.GetAllActiveAuthConfigsAsync(Arg.Any<CancellationToken>()).Returns(configs);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<TestMinistryConnectionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new TestMinistryConnectionResponse(true, 200, 10, null));

        var sut = Build(repository, mediator);
        await sut.SweepAsync(CancellationToken.None);

        foreach (var config in configs)
            await mediator.Received(1).Send(
                Arg.Is<TestMinistryConnectionCommand>(c => c.AuthConfigId == config.Id),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepAsync_OneConfigThrows_StillTestsTheRest()
    {
        var throwing = BuildConfig();
        var healthy = BuildConfig();
        var repository = Substitute.For<IMinistryRepository>();
        repository.GetAllActiveAuthConfigsAsync(Arg.Any<CancellationToken>())
            .Returns([throwing, healthy]);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(
                Arg.Is<TestMinistryConnectionCommand>(c => c.AuthConfigId == throwing.Id),
                Arg.Any<CancellationToken>())
            .Returns<TestMinistryConnectionResponse>(_ => throw new InvalidOperationException("adapter blew up"));
        mediator.Send(
                Arg.Is<TestMinistryConnectionCommand>(c => c.AuthConfigId == healthy.Id),
                Arg.Any<CancellationToken>())
            .Returns(new TestMinistryConnectionResponse(true, 200, 5, null));

        var sut = Build(repository, mediator);
        var act = () => sut.SweepAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        await mediator.Received(1).Send(
            Arg.Is<TestMinistryConnectionCommand>(c => c.AuthConfigId == healthy.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepAsync_NoActiveConfigs_CompletesWithoutSendingAnyCommand()
    {
        var repository = Substitute.For<IMinistryRepository>();
        repository.GetAllActiveAuthConfigsAsync(Arg.Any<CancellationToken>())
            .Returns([]);
        var mediator = Substitute.For<IMediator>();

        var sut = Build(repository, mediator);
        await sut.SweepAsync(CancellationToken.None);

        await mediator.DidNotReceive().Send(Arg.Any<TestMinistryConnectionCommand>(), Arg.Any<CancellationToken>());
    }
}
