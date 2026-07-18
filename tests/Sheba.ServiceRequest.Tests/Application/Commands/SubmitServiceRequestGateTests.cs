using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.ServiceRequest.Application.Commands.SubmitServiceRequest;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.ServiceRequest.Tests.Application.Commands;

/// <summary>Submission-gate tests for SubmitServiceRequestHandler (T-SRV-3, BR-SR-2).</summary>
public sealed class SubmitServiceRequestGateTests
{
    private readonly IServiceDefinitionRepository _defRepo = Substitute.For<IServiceDefinitionRepository>();
    private readonly IServiceRequestRepository _reqRepo = Substitute.For<IServiceRequestRepository>();
    private readonly IDocumentPort _documentPort = Substitute.For<IDocumentPort>();
    private readonly SubmitServiceRequestHandler _sut;
    private readonly Guid _serviceId = Guid.NewGuid();
    private readonly Guid _citizenId = Guid.NewGuid();

    public SubmitServiceRequestGateTests()
    {
        _sut = new SubmitServiceRequestHandler(
            _defRepo, _reqRepo, _documentPort, NullLogger<SubmitServiceRequestHandler>.Instance);
        _documentPort.GetOwnerDocumentTypesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>() as IReadOnlySet<string>);
    }

    private ServiceDefinition PublishedService(int requiredLoa)
    {
        var service = ServiceDefinition.Create(Guid.NewGuid(), Guid.NewGuid(), "SVC", "خدمة", "Service", requiredLoa: requiredLoa);
        // Publish() needs a form schema or workflow step; give it a schema.
        service.SetFormSchema(ServiceFormSchema.Create(service.Id, "{}"));
        service.Publish();
        _defRepo.GetServiceByIdAsync(_serviceId, Arg.Any<CancellationToken>()).Returns(service);
        return service;
    }

    [Fact]
    public async Task Handle_CitizenLoaBelowRequired_Throws()
    {
        PublishedService(requiredLoa: 2);
        var command = new SubmitServiceRequestCommand(_serviceId, _citizenId, "{}", CitizenLoa: 1);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Handle_CitizenLoaMeetsRequired_Succeeds()
    {
        PublishedService(requiredLoa: 2);
        var command = new SubmitServiceRequestCommand(_serviceId, _citizenId, "{}", CitizenLoa: 2);

        var result = await _sut.Handle(command, default);

        result.Should().NotBeNull();
        await _reqRepo.Received(1).AddAsync(Arg.Any<ServiceRequestEntity>(), Arg.Any<CancellationToken>());
    }
}
