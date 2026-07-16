using Sheba.ServiceRequest.Domain.Enums;
using Sheba.Shared.Kernel.Entities;

namespace Sheba.ServiceRequest.Domain.Entities;

/// <summary>
/// A step in the service workflow definition.
/// Defines the order, type, actor, and configuration for each step.
/// </summary>
public sealed class ServiceWorkflowStep : BaseEntity
{
    public Guid ServiceId { get; private set; }
    public int StepOrder { get; private set; }
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public WorkflowStepType StepType { get; private set; }
    public WorkflowActor Actor { get; private set; }
    public Guid? MinistryEndpointId { get; private set; }     // logical FK to ministry_db
    public int? TimeoutHours { get; private set; }
    public bool IsAutomated { get; private set; }
    public int? OnSuccessStep { get; private set; }
    public int? OnFailureStep { get; private set; }
    public string? ConfigJson { get; private set; }            // step-specific configuration

    private ServiceWorkflowStep() { }

    public static ServiceWorkflowStep Create(
        Guid serviceId, int stepOrder, string nameAr, string nameEn,
        WorkflowStepType stepType, WorkflowActor actor,
        Guid? ministryEndpointId = null, int? timeoutHours = null,
        bool isAutomated = false, int? onSuccessStep = null,
        int? onFailureStep = null, string? configJson = null)
    {
        return new ServiceWorkflowStep
        {
            ServiceId = serviceId,
            StepOrder = stepOrder,
            NameAr = nameAr.Trim(),
            NameEn = nameEn.Trim(),
            StepType = stepType,
            Actor = actor,
            MinistryEndpointId = ministryEndpointId,
            TimeoutHours = timeoutHours,
            IsAutomated = isAutomated,
            OnSuccessStep = onSuccessStep,
            OnFailureStep = onFailureStep,
            ConfigJson = configJson
        };
    }
}
