using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Enums;

namespace Sheba.ServiceRequest.Domain.Interfaces;

/// <summary>
/// Repository for service request runtime entities (citizen requests + step executions).
/// </summary>
public interface IServiceRequestRepository
{
    Task<ServiceRequestEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ServiceRequestEntity?> GetByReferenceAsync(string referenceNumber, CancellationToken ct = default);
    Task<List<ServiceRequestEntity>> GetByCitizenAsync(Guid citizenId, CancellationToken ct = default);
    Task<List<ServiceRequestEntity>> GetAllAsync(
        RequestLifecycleStatus? status = null, Guid? serviceId = null, Guid? ministryId = null,
        DateTime? fromDate = null, DateTime? toDate = null,
        int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<int> CountAllAsync(
        RequestLifecycleStatus? status = null, Guid? serviceId = null, Guid? ministryId = null,
        DateTime? fromDate = null, DateTime? toDate = null, CancellationToken ct = default);
    Task AddAsync(ServiceRequestEntity request, CancellationToken ct = default);

    /// <summary>AwaitingMinistry requests past their DueDate (BR-SR-6 SLA sweep, T-SRV-3).</summary>
    Task<List<ServiceRequestEntity>> GetOverdueAwaitingMinistryRequestsAsync(DateTime asOf, CancellationToken ct = default);

    // Step executions
    Task<RequestStepExecution?> GetStepExecutionByIdAsync(Guid id, CancellationToken ct = default);
    Task<RequestStepExecution?> GetActiveStepForRequestAsync(Guid requestId, CancellationToken ct = default);
    Task<List<RequestStepExecution>> GetStepExecutionsByRequestAsync(Guid requestId, CancellationToken ct = default);
    Task AddStepExecutionAsync(RequestStepExecution execution, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
