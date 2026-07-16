namespace Sheba.ServiceRequest.Domain.Enums;

/// <summary>Types of workflow steps in a service definition.</summary>
public enum WorkflowStepType
{
    CitizenSubmit = 1,
    Payment = 2,
    MinistryApiCall = 3,
    MinistryReview = 4,
    AdminReview = 5,
    DocumentIssue = 6,
    Notification = 7,
    WebhookWait = 8,
    Appointment = 9
}

/// <summary>Who executes a workflow step.</summary>
public enum WorkflowActor
{
    Citizen = 1,
    System = 2,
    Ministry = 3,
    ShebaAdmin = 4
}

/// <summary>Lifecycle status of a citizen's service request.</summary>
public enum RequestLifecycleStatus
{
    Draft = 1,
    Submitted = 2,
    PaymentPending = 3,
    UnderReview = 4,
    Processing = 5,
    AwaitingMinistry = 6,
    ActionRequired = 7,
    Completed = 8,
    Rejected = 9,
    Cancelled = 10,
    Expired = 11
}
