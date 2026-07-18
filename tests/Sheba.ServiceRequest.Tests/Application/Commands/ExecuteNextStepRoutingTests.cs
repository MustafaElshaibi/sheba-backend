using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.ServiceRequest.Application.Commands.ExecuteNextStep;
using Sheba.ServiceRequest.Application.StepHandlers;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Enums;
using Sheba.ServiceRequest.Domain.Interfaces;

namespace Sheba.ServiceRequest.Tests.Application.Commands;

/// <summary>T-SRV-4: unhandled step types fail loudly; on_failure_step is honored.</summary>
public sealed class ExecuteNextStepRoutingTests
{
    private readonly IServiceRequestRepository _reqRepo = Substitute.For<IServiceRequestRepository>();
    private readonly IServiceDefinitionRepository _defRepo = Substitute.For<IServiceDefinitionRepository>();

    private ServiceRequestEntity _request = null!;

    private ExecuteNextStepHandler Build(IEnumerable<IWorkflowStepHandler> handlers, List<ServiceWorkflowStep> steps)
    {
        _request = ServiceRequestEntity.Create(Guid.NewGuid(), Guid.NewGuid(), "{}", averageDays: 5);
        _reqRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_request);
        _reqRepo.GetStepExecutionForStepAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((RequestStepExecution?)null);
        _defRepo.GetWorkflowStepsByServiceAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(steps);
        return new ExecuteNextStepHandler(_reqRepo, _defRepo, handlers, NullLogger<ExecuteNextStepHandler>.Instance);
    }

    private static ServiceWorkflowStep Step(int order, WorkflowStepType type, int? onFailure = null, bool automated = true) =>
        ServiceWorkflowStep.Create(Guid.NewGuid(), order, "خطوة", "Step", type, WorkflowActor.System,
            isAutomated: automated, onFailureStep: onFailure);

    [Fact]
    public async Task Handle_UnhandledStepType_MarksActionRequired_DoesNotAutoComplete()
    {
        var steps = new List<ServiceWorkflowStep> { Step(1, WorkflowStepType.MinistryApiCall) };
        var sut = Build(handlers: [], steps); // no handler registered for MinistryApiCall

        var result = await sut.Handle(new ExecuteNextStepCommand(_request.Id), default);

        _request.Status.Should().Be(RequestLifecycleStatus.ActionRequired);
        result.Status.Should().Be(RequestLifecycleStatus.ActionRequired.ToString());
    }

    [Fact]
    public async Task Handle_StepFailsWithOnFailureStep_RoutesToThatStep()
    {
        // step 1 fails and routes to step 2 (a non-automated review step, so it pauses there)
        var steps = new List<ServiceWorkflowStep>
        {
            Step(1, WorkflowStepType.MinistryApiCall, onFailure: 2),
            Step(2, WorkflowStepType.AdminReview, automated: false),
        };
        var failing = Substitute.For<IWorkflowStepHandler>();
        failing.StepType.Returns(WorkflowStepType.MinistryApiCall);
        failing.ExecuteAsync(Arg.Any<ServiceRequestEntity>(), Arg.Any<ServiceWorkflowStep>(), Arg.Any<RequestStepExecution>(), Arg.Any<CancellationToken>())
            .Returns(new StepExecutionResult(false, false, ErrorMessage: "ministry down"));
        var sut = Build([failing], steps);

        await sut.Handle(new ExecuteNextStepCommand(_request.Id), default);

        _request.CurrentStep.Should().Be(2);
        _request.Status.Should().NotBe(RequestLifecycleStatus.ActionRequired);
    }

    [Fact]
    public async Task Handle_StepFailsWithNoFailureStep_MarksActionRequired()
    {
        var steps = new List<ServiceWorkflowStep> { Step(1, WorkflowStepType.MinistryApiCall, onFailure: null) };
        var failing = Substitute.For<IWorkflowStepHandler>();
        failing.StepType.Returns(WorkflowStepType.MinistryApiCall);
        failing.ExecuteAsync(Arg.Any<ServiceRequestEntity>(), Arg.Any<ServiceWorkflowStep>(), Arg.Any<RequestStepExecution>(), Arg.Any<CancellationToken>())
            .Returns(new StepExecutionResult(false, false, ErrorMessage: "boom"));
        var sut = Build([failing], steps);

        await sut.Handle(new ExecuteNextStepCommand(_request.Id), default);

        _request.Status.Should().Be(RequestLifecycleStatus.ActionRequired);
    }
}
