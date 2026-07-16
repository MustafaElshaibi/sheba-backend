using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.ServiceRequest.Domain.Entities;

/// <summary>
/// A government service definition — what a citizen can request.
/// Links to category, ministry (logical FK), form schema, fees, workflow steps.
/// </summary>
public sealed class ServiceDefinition : BaseEntity
{
    public Guid CategoryId { get; private set; }
    public Guid MinistryId { get; private set; }          // logical FK to ministry_db
    public string Code { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? DescriptionAr { get; private set; }
    public string? DescriptionEn { get; private set; }
    public string? EligibilityRulesJson { get; private set; }
    public int RequiredLoa { get; private set; } = 1;
    public bool RequiresAppointment { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsOnline { get; private set; } = true;
    public int? AverageDays { get; private set; }
    public int DisplayOrder { get; private set; }
    public string? TagsCsv { get; private set; }            // stored as text[], mapped via value converter
    public string? MetadataJson { get; private set; }

    // Navigation
    public ServiceFormSchema? FormSchema { get; private set; }

    private readonly List<ServiceFee> _fees = [];
    public IReadOnlyCollection<ServiceFee> Fees => _fees.AsReadOnly();

    private readonly List<ServiceRequiredDocument> _requiredDocuments = [];
    public IReadOnlyCollection<ServiceRequiredDocument> RequiredDocuments => _requiredDocuments.AsReadOnly();

    private readonly List<ServiceWorkflowStep> _workflowSteps = [];
    public IReadOnlyCollection<ServiceWorkflowStep> WorkflowSteps => _workflowSteps.AsReadOnly();

    private ServiceDefinition() { }

    public static ServiceDefinition Create(
        Guid categoryId,
        Guid ministryId,
        string code,
        string nameAr,
        string nameEn,
        int requiredLoa = 1,
        string? descriptionAr = null,
        string? descriptionEn = null,
        int? averageDays = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("Service code is required.");
        if (string.IsNullOrWhiteSpace(nameAr)) throw new DomainException("Arabic name is required.");
        if (string.IsNullOrWhiteSpace(nameEn)) throw new DomainException("English name is required.");

        return new ServiceDefinition
        {
            CategoryId = categoryId,
            MinistryId = ministryId,
            Code = code.Trim().ToUpperInvariant(),
            NameAr = nameAr.Trim(),
            NameEn = nameEn.Trim(),
            DescriptionAr = descriptionAr,
            DescriptionEn = descriptionEn,
            RequiredLoa = requiredLoa is >= 1 and <= 3 ? requiredLoa : 1,
            AverageDays = averageDays,
            IsActive = false     // start unpublished
        };
    }

    public void Update(string nameAr, string nameEn, string? descriptionAr, string? descriptionEn,
        int requiredLoa, bool requiresAppointment, bool isOnline, int? averageDays, int displayOrder)
    {
        NameAr = nameAr.Trim(); NameEn = nameEn.Trim();
        DescriptionAr = descriptionAr; DescriptionEn = descriptionEn;
        RequiredLoa = requiredLoa; RequiresAppointment = requiresAppointment;
        IsOnline = isOnline; AverageDays = averageDays; DisplayOrder = displayOrder;
        Touch();
    }

    /// <summary>Publishes the service — makes it visible in the citizen catalog.</summary>
    public void Publish()
    {
        if (FormSchema is null && _workflowSteps.Count == 0)
            throw new DomainException("Cannot publish a service without a form schema or workflow steps.");
        IsActive = true;
        Touch();
    }

    /// <summary>Takes the service offline — citizens can no longer submit new requests.</summary>
    public void Depublish() { IsActive = false; Touch(); }

    public void SetFormSchema(ServiceFormSchema schema) { FormSchema = schema; Touch(); }
}
