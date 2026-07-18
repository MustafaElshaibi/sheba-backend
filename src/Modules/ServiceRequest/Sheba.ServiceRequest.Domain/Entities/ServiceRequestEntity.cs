using Sheba.ServiceRequest.Domain.DomainEvents;
using Sheba.ServiceRequest.Domain.Enums;
using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Events.IntegrationEvents;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.ServiceRequest.Domain.Entities;

/// <summary>
/// A citizen's actual request for a government service.
/// Tracks the lifecycle from submission through workflow execution to completion.
/// </summary>
public sealed class ServiceRequestEntity : BaseEntity
{
    public string ReferenceNumber { get; private set; } = string.Empty;
    public Guid ServiceId { get; private set; }
    public Guid CitizenId { get; private set; }
    public RequestLifecycleStatus Status { get; private set; } = RequestLifecycleStatus.Draft;
    public int CurrentStep { get; private set; } = 1;
    public string? FormDataJson { get; private set; }
    public string Priority { get; private set; } = "NORMAL";
    public DateTime SubmittedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; private set; }
    public DateTime? DueDate { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? Notes { get; private set; }

    private readonly List<RequestStepExecution> _stepExecutions = [];
    public IReadOnlyCollection<RequestStepExecution> StepExecutions => _stepExecutions.AsReadOnly();

    private ServiceRequestEntity() { }

    /// <summary>Creates a new service request from a citizen submission.</summary>
    public static ServiceRequestEntity Create(
        Guid serviceId,
        Guid citizenId,
        string formDataJson,
        string priority = "NORMAL",
        int? averageDays = null)
    {
        var refNum = $"SHB-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        var request = new ServiceRequestEntity
        {
            ServiceId = serviceId,
            CitizenId = citizenId,
            FormDataJson = formDataJson,
            Priority = priority.ToUpperInvariant(),
            Status = RequestLifecycleStatus.Submitted,
            ReferenceNumber = refNum,
            SubmittedAt = DateTime.UtcNow,
            DueDate = averageDays.HasValue ? DateTime.UtcNow.AddDays(averageDays.Value) : null
        };

        request.RaiseDomainEvent(new ServiceRequestSubmittedEvent(
            request.Id, request.ServiceId, request.CitizenId, request.ReferenceNumber));

        return request;
    }

    /// <summary>Raises a WorkflowStepCompletedEvent for the given step execution.</summary>
    public void RaiseStepCompleted(Guid stepExecutionId, int stepOrder, string stepType)
        => RaiseDomainEvent(new WorkflowStepCompletedEvent(Id, stepExecutionId, stepOrder, stepType));

    private static readonly HashSet<RequestLifecycleStatus> TerminalStatuses =
    [
        RequestLifecycleStatus.Completed,
        RequestLifecycleStatus.Rejected,
        RequestLifecycleStatus.Cancelled,
        RequestLifecycleStatus.Expired
    ];

    /// <summary>T-SRV-3: no transition is legal out of a terminal status — a completed, rejected,
    /// cancelled, or expired request is immutable (BR-SR-7).</summary>
    private void EnsureNotTerminal(string action)
    {
        if (TerminalStatuses.Contains(Status))
            throw new DomainException($"Cannot {action} a request in terminal status {Status}.");
    }

    public void AdvanceToStep(int stepOrder, RequestLifecycleStatus newStatus)
    {
        EnsureNotTerminal("advance");
        CurrentStep = stepOrder;
        Status = newStatus;
        Touch();
    }

    public void MarkPaymentPending() { EnsureNotTerminal("move to payment-pending"); Status = RequestLifecycleStatus.PaymentPending; Touch(); }
    public void MarkProcessing() { EnsureNotTerminal("move to processing"); Status = RequestLifecycleStatus.Processing; Touch(); }
    public void MarkAwaitingMinistry() { EnsureNotTerminal("move to awaiting-ministry"); Status = RequestLifecycleStatus.AwaitingMinistry; Touch(); }
    public void MarkUnderReview() { EnsureNotTerminal("move to under-review"); Status = RequestLifecycleStatus.UnderReview; Touch(); }

    public void Complete()
    {
        EnsureNotTerminal("complete");
        Status = RequestLifecycleStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        Touch();

        RaiseDomainEvent(new ServiceRequestCompletedEvent(
            Id, ServiceId, CitizenId, SubmittedAt, CompletedAt.Value));
    }

    public void Reject(string reason)
    {
        EnsureNotTerminal("reject");
        Status = RequestLifecycleStatus.Rejected;
        RejectionReason = reason;
        CompletedAt = DateTime.UtcNow;
        Touch();
    }

    public void Cancel()
    {
        EnsureNotTerminal("cancel");
        Status = RequestLifecycleStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
        Touch();
    }

    /// <summary>SLA sweep (BR-SR-6, T-SRV-3): an abandoned AwaitingMinistry request past its
    /// due date is expired. Only legal from AwaitingMinistry — the sweep job itself enforces that,
    /// this is the aggregate-level backstop.</summary>
    public void Expire()
    {
        if (Status != RequestLifecycleStatus.AwaitingMinistry)
            throw new DomainException($"Cannot expire a request in status {Status}. Only AwaitingMinistry requests are expired by the SLA sweep.");

        Status = RequestLifecycleStatus.Expired;
        CompletedAt = DateTime.UtcNow;
        Touch();
    }
}
