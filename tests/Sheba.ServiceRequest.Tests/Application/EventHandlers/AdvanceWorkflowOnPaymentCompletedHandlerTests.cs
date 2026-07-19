using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.ServiceRequest.Application.Commands.ExecuteNextStep;
using Sheba.ServiceRequest.Application.EventHandlers;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Enums;
using Sheba.ServiceRequest.Domain.Interfaces;
using Sheba.Shared.Kernel.Events.IntegrationEvents;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.ServiceRequest.Tests.Application.EventHandlers;

/// <summary>T-PAY-1: PaymentCompletedEvent replaces the old direct MarkPaymentComplete coupling.</summary>
public sealed class AdvanceWorkflowOnPaymentCompletedHandlerTests
{
    private readonly IServiceRequestRepository _reqRepo = Substitute.For<IServiceRequestRepository>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IInboxGuard _inboxGuard = Substitute.For<IInboxGuard>();

    private AdvanceWorkflowOnPaymentCompletedHandler Build() =>
        new(_reqRepo, _mediator, _inboxGuard,
            NullLogger<AdvanceWorkflowOnPaymentCompletedHandler>.Instance);

    private static PaymentCompletedEvent Event(Guid requestId) =>
        new(Guid.NewGuid(), requestId, Guid.NewGuid(), 500m, "YER", "GW-REF", DateTime.UtcNow);

    [Fact]
    public async Task Handle_AlreadyProcessed_SkipsEntirely()
    {
        var evt = Event(Guid.NewGuid());
        _inboxGuard.IsProcessedAsync(evt.EventId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        await Build().Handle(evt, default);

        await _reqRepo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PaidOrder_CompletesActiveStepAndAdvancesWorkflow()
    {
        var request = ServiceRequestEntity.Create(Guid.NewGuid(), Guid.NewGuid(), "{}", averageDays: 5);
        request.MarkPaymentPending(); // step 1 is the Payment step; workflow paused here
        var evt = Event(request.Id);

        _inboxGuard.IsProcessedAsync(evt.EventId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _reqRepo.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        var activeStep = RequestStepExecution.Create(request.Id, Guid.NewGuid(), 1);
        _reqRepo.GetActiveStepForRequestAsync(request.Id, Arg.Any<CancellationToken>()).Returns(activeStep);

        await Build().Handle(evt, default);

        activeStep.Status.Should().Be(StepExecutionStatus.Completed);
        request.CurrentStep.Should().Be(2);
        request.Status.Should().Be(RequestLifecycleStatus.Processing);
        await _reqRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _inboxGuard.Received(1).MarkProcessedAsync(evt.EventId, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(Arg.Any<ExecuteNextStepCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RequestNotFound_MarksProcessed_DoesNotThrow()
    {
        var evt = Event(Guid.NewGuid());
        _inboxGuard.IsProcessedAsync(evt.EventId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _reqRepo.GetByIdAsync(evt.ServiceRequestId, Arg.Any<CancellationToken>()).Returns((ServiceRequestEntity?)null);

        await Build().Handle(evt, default);

        await _inboxGuard.Received(1).MarkProcessedAsync(evt.EventId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
