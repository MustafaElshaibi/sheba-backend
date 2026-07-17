using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.ServiceRequest.Application.Commands.UpdateServiceDefinition;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.ServiceRequest.Tests.Application.Commands;

/// <summary>T-AUTH-1: a MinistryManager may only update services their own ministry owns.</summary>
public sealed class UpdateServiceDefinitionOwnershipTests
{
    private readonly IServiceDefinitionRepository _repo = Substitute.For<IServiceDefinitionRepository>();
    private readonly UpdateServiceDefinitionHandler _sut;

    public UpdateServiceDefinitionOwnershipTests()
        => _sut = new UpdateServiceDefinitionHandler(_repo, NullLogger<UpdateServiceDefinitionHandler>.Instance);

    private static ServiceDefinition MakeService(Guid ministryId) =>
        ServiceDefinition.Create(Guid.NewGuid(), ministryId, "SVC1", "خدمة", "Service");

    private UpdateServiceDefinitionCommand MakeCommand(Guid serviceId, Guid? actorMinistryId) =>
        new(serviceId, "اسم جديد", "New Name", ActorMinistryId: actorMinistryId);

    [Fact]
    public async Task Handle_SuperAdmin_NoMinistryRestriction_Succeeds()
    {
        var service = MakeService(Guid.NewGuid());
        _repo.GetServiceByIdAsync(service.Id, default).Returns(service);

        var result = await _sut.Handle(MakeCommand(service.Id, actorMinistryId: null), default);

        result.ServiceId.Should().Be(service.Id);
    }

    [Fact]
    public async Task Handle_MinistryManager_OwnService_Succeeds()
    {
        var ministryId = Guid.NewGuid();
        var service = MakeService(ministryId);
        _repo.GetServiceByIdAsync(service.Id, default).Returns(service);

        var result = await _sut.Handle(MakeCommand(service.Id, actorMinistryId: ministryId), default);

        result.ServiceId.Should().Be(service.Id);
    }

    [Fact]
    public async Task Handle_MinistryManager_OtherMinistrysService_ThrowsNotFound()
    {
        var service = MakeService(Guid.NewGuid()); // owned by a different ministry
        _repo.GetServiceByIdAsync(service.Id, default).Returns(service);
        var actorMinistryId = Guid.NewGuid();

        var act = () => _sut.Handle(MakeCommand(service.Id, actorMinistryId), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
