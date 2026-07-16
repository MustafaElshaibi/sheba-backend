using Sheba.ServiceRequest.Domain.Entities;

namespace Sheba.ServiceRequest.Domain.Interfaces;

/// <summary>
/// Repository abstraction for service catalog entities.
/// </summary>
public interface IServiceDefinitionRepository
{
    // ── Category ──────────────────────────────────────────────────────────
    Task<ServiceCategory?> GetCategoryByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<ServiceCategory>> GetAllCategoriesAsync(bool includeInactive = false, CancellationToken ct = default);
    Task AddCategoryAsync(ServiceCategory category, CancellationToken ct = default);

    // ── ServiceDefinition ─────────────────────────────────────────────────
    Task<ServiceDefinition?> GetServiceByIdAsync(Guid id, CancellationToken ct = default);
    Task<ServiceDefinition?> GetServiceByCodeAsync(string code, CancellationToken ct = default);
    Task<List<ServiceDefinition>> GetServicesByCategoryAsync(Guid categoryId, CancellationToken ct = default);
    Task<List<ServiceDefinition>> GetAllServicesAsync(bool includeInactive = false, CancellationToken ct = default);
    Task AddServiceAsync(ServiceDefinition service, CancellationToken ct = default);

    // ── FormSchema ────────────────────────────────────────────────────────
    Task AddFormSchemaAsync(ServiceFormSchema schema, CancellationToken ct = default);
    Task<ServiceFormSchema?> GetFormSchemaByServiceIdAsync(Guid serviceId, CancellationToken ct = default);

    // ── Fee ───────────────────────────────────────────────────────────────
    Task AddFeeAsync(ServiceFee fee, CancellationToken ct = default);
    Task<List<ServiceFee>> GetFeesByServiceAsync(Guid serviceId, CancellationToken ct = default);

    // ── WorkflowStep ──────────────────────────────────────────────────────
    Task AddWorkflowStepAsync(ServiceWorkflowStep step, CancellationToken ct = default);
    Task<List<ServiceWorkflowStep>> GetWorkflowStepsByServiceAsync(Guid serviceId, CancellationToken ct = default);

    // ── RequiredDocument ──────────────────────────────────────────────────
    Task AddRequiredDocumentAsync(ServiceRequiredDocument doc, CancellationToken ct = default);

    // ── UoW ───────────────────────────────────────────────────────────────
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
