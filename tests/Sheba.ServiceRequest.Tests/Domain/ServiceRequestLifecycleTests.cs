using FluentAssertions;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Enums;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.ServiceRequest.Tests.Domain;

/// <summary>Transition-guard tests for ServiceRequestEntity (T-SRV-3 / BR-SR-7).</summary>
public sealed class ServiceRequestLifecycleTests
{
    private static ServiceRequestEntity NewRequest() =>
        ServiceRequestEntity.Create(Guid.NewGuid(), Guid.NewGuid(), "{}", averageDays: 5);

    [Fact]
    public void Complete_FromTerminalCancelled_Throws()
    {
        var request = NewRequest();
        request.Cancel();

        var act = () => request.Complete();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Cancel_AfterCompletion_Throws()
    {
        var request = NewRequest();
        request.Complete();

        var act = () => request.Cancel();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Cancel_BeforeCompletion_Succeeds()
    {
        var request = NewRequest();

        request.Cancel();

        request.Status.Should().Be(RequestLifecycleStatus.Cancelled);
    }

    [Fact]
    public void Expire_OnlyFromAwaitingMinistry()
    {
        var request = NewRequest(); // Submitted

        var act = () => request.Expire();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Expire_FromAwaitingMinistry_Succeeds()
    {
        var request = NewRequest();
        request.MarkAwaitingMinistry();

        request.Expire();

        request.Status.Should().Be(RequestLifecycleStatus.Expired);
    }

    [Fact]
    public void MarkProcessing_AfterExpiry_Throws()
    {
        var request = NewRequest();
        request.MarkAwaitingMinistry();
        request.Expire();

        var act = () => request.MarkProcessing();

        act.Should().Throw<DomainException>();
    }
}
