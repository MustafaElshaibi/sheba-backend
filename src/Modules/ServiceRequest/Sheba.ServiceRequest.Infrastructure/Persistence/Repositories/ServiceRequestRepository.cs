using Microsoft.EntityFrameworkCore;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Enums;
using Sheba.ServiceRequest.Domain.Interfaces;

namespace Sheba.ServiceRequest.Infrastructure.Persistence.Repositories;

public sealed class ServiceRequestRepository(ServiceRequestDbContext db) : IServiceRequestRepository
{
    public async Task<ServiceRequestEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.ServiceRequests.Include(r => r.StepExecutions).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<ServiceRequestEntity?> GetByReferenceAsync(string referenceNumber, CancellationToken ct = default)
        => await db.ServiceRequests.FirstOrDefaultAsync(r => r.ReferenceNumber == referenceNumber, ct);

    public async Task<List<ServiceRequestEntity>> GetByCitizenAsync(Guid citizenId, CancellationToken ct = default)
        => await db.ServiceRequests.Where(r => r.CitizenId == citizenId)
            .OrderByDescending(r => r.SubmittedAt).ToListAsync(ct);

    public async Task<List<ServiceRequestEntity>> GetAllAsync(
        RequestLifecycleStatus? status, Guid? serviceId, Guid? ministryId,
        DateTime? fromDate, DateTime? toDate,
        int page, int pageSize, CancellationToken ct = default)
    {
        var q = BuildFilterQuery(status, serviceId, fromDate, toDate);
        return await q.OrderByDescending(r => r.SubmittedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    }

    public async Task<int> CountAllAsync(
        RequestLifecycleStatus? status, Guid? serviceId, Guid? ministryId,
        DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        return await BuildFilterQuery(status, serviceId, fromDate, toDate).CountAsync(ct);
    }

    public async Task AddAsync(ServiceRequestEntity request, CancellationToken ct = default)
        => await db.ServiceRequests.AddAsync(request, ct);

    public async Task<List<ServiceRequestEntity>> GetOverdueAwaitingMinistryRequestsAsync(
        DateTime asOf, CancellationToken ct = default)
    {
        return await db.ServiceRequests
            .Where(r => r.Status == RequestLifecycleStatus.AwaitingMinistry
                     && r.DueDate != null && r.DueDate < asOf)
            .ToListAsync(ct);
    }

    public async Task<RequestStepExecution?> GetStepExecutionByIdAsync(Guid id, CancellationToken ct = default)
        => await db.StepExecutions.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<RequestStepExecution?> GetActiveStepForRequestAsync(Guid requestId, CancellationToken ct = default)
        => await db.StepExecutions.Where(e => e.RequestId == requestId && e.Status == StepExecutionStatus.Running)
            .FirstOrDefaultAsync(ct);

    public async Task<List<RequestStepExecution>> GetStepExecutionsByRequestAsync(Guid requestId, CancellationToken ct = default)
        => await db.StepExecutions.Where(e => e.RequestId == requestId)
            .OrderBy(e => e.StepOrder).ToListAsync(ct);

    public async Task AddStepExecutionAsync(RequestStepExecution execution, CancellationToken ct = default)
        => await db.StepExecutions.AddAsync(execution, ct);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);

    private IQueryable<ServiceRequestEntity> BuildFilterQuery(
        RequestLifecycleStatus? status, Guid? serviceId, DateTime? fromDate, DateTime? toDate)
    {
        var q = db.ServiceRequests.AsQueryable();
        if (status.HasValue) q = q.Where(r => r.Status == status.Value);
        if (serviceId.HasValue) q = q.Where(r => r.ServiceId == serviceId.Value);
        if (fromDate.HasValue) q = q.Where(r => r.SubmittedAt >= fromDate.Value);
        if (toDate.HasValue) q = q.Where(r => r.SubmittedAt <= toDate.Value);
        return q;
    }
}
