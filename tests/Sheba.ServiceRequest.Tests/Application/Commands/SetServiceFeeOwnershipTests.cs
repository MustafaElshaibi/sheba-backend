using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.ServiceRequest.Application.Commands.SetServiceFee;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.ServiceRequest.Tests.Application.Commands;

/// <summary>T-AUTH-1: a MinistryManager may only add fees to services their own ministry owns.</summary>
public sealed class SetServiceFeeOwnershipTests
{
    private readonly IServiceDefinitionRepository _repo = Substitute.For<IServiceDefinitionRepository>();
    private readonly SetServiceFeeHandler _sut;

    public SetServiceFeeOwnershipTests()
        => _sut = new SetServiceFeeHandler(_repo, NullLogger<SetServiceFeeHandler>.Instance);

    private static ServiceDefinition MakeService(Guid ministryId) =>
        ServiceDefinition.Create(Guid.NewGuid(), ministryId, "SVC1", "خدمة", "Service");

    private SetServiceFeeCommand MakeCommand(Guid serviceId, Guid? actorMinistryId) =>
        new(serviceId, "BASE", "رسوم", "Fee", 100m, ActorMinistryId: actorMinistryId);

    [Fact]
    public async Task Handle_MinistryManager_OwnService_Succeeds()
    {
        var ministryId = Guid.NewGuid();
        var service = MakeService(ministryId);
        _repo.GetServiceByIdAsync(service.Id, default).Returns(service);

        var result = await _sut.Handle(MakeCommand(service.Id, ministryId), default);

        result.FeeId.Should().NotBe(Guid.Empty);
        await _repo.Received(1).AddFeeAsync(Arg.Any<ServiceFee>(), default);
    }

    [Fact]
    public async Task Handle_MinistryManager_OtherMinistrysService_ThrowsNotFound()
    {
        var service = MakeService(Guid.NewGuid());
        _repo.GetServiceByIdAsync(service.Id, default).Returns(service);

        var act = () => _sut.Handle(MakeCommand(service.Id, Guid.NewGuid()), default);

        await act.Should().ThrowAsync<NotFoundException>();
        await _repo.DidNotReceive().AddFeeAsync(Arg.Any<ServiceFee>(), default);
    }
}
