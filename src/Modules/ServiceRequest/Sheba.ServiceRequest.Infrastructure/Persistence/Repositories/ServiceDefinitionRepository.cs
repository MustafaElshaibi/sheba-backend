using Microsoft.EntityFrameworkCore;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Interfaces;

namespace Sheba.ServiceRequest.Infrastructure.Persistence.Repositories;

public sealed class ServiceDefinitionRepository(ServiceRequestDbContext db) : IServiceDefinitionRepository
{
    // ── Category ──────────────────────────────────────────────────────────
    public async Task<ServiceCategory?> GetCategoryByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Categories.Include(c => c.Services).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<List<ServiceCategory>> GetAllCategoriesAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var q = db.Categories.Include(c => c.Services).AsQueryable();
        if (!includeInactive) q = q.Where(c => c.IsActive);
        return await q.OrderBy(c => c.DisplayOrder).ThenBy(c => c.NameEn).ToListAsync(ct);
    }

    public async Task AddCategoryAsync(ServiceCategory category, CancellationToken ct = default)
        => await db.Categories.AddAsync(category, ct);

    // ── ServiceDefinition ─────────────────────────────────────────────────
    public async Task<ServiceDefinition?> GetServiceByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Services
            .Include(s => s.FormSchema)
            .Include(s => s.Fees)
            .Include(s => s.RequiredDocuments)
            .Include(s => s.WorkflowSteps)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<ServiceDefinition?> GetServiceByCodeAsync(string code, CancellationToken ct = default)
        => await db.Services.FirstOrDefaultAsync(s => s.Code == code, ct);

    public async Task<List<ServiceDefinition>> GetServicesByCategoryAsync(Guid categoryId, CancellationToken ct = default)
        => await db.Services
            .Include(s => s.Fees)
            .Where(s => s.CategoryId == categoryId && s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync(ct);

    public async Task<List<ServiceDefinition>> GetAllServicesAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var q = db.Services.Include(s => s.Fees).AsQueryable();
        if (!includeInactive) q = q.Where(s => s.IsActive);
        return await q.OrderBy(s => s.DisplayOrder).ThenBy(s => s.NameEn).ToListAsync(ct);
    }

    public async Task AddServiceAsync(ServiceDefinition service, CancellationToken ct = default)
        => await db.Services.AddAsync(service, ct);

    // ── FormSchema ────────────────────────────────────────────────────────
    public async Task AddFormSchemaAsync(ServiceFormSchema schema, CancellationToken ct = default)
        => await db.FormSchemas.AddAsync(schema, ct);

    public async Task<ServiceFormSchema?> GetFormSchemaByServiceIdAsync(Guid serviceId, CancellationToken ct = default)
        => await db.FormSchemas.FirstOrDefaultAsync(f => f.ServiceId == serviceId, ct);

    // ── Fee ───────────────────────────────────────────────────────────────
    public async Task AddFeeAsync(ServiceFee fee, CancellationToken ct = default)
        => await db.Fees.AddAsync(fee, ct);

    public async Task<List<ServiceFee>> GetFeesByServiceAsync(Guid serviceId, CancellationToken ct = default)
        => await db.Fees.Where(f => f.ServiceId == serviceId).ToListAsync(ct);

    // ── WorkflowStep ──────────────────────────────────────────────────────
    public async Task AddWorkflowStepAsync(ServiceWorkflowStep step, CancellationToken ct = default)
        => await db.WorkflowSteps.AddAsync(step, ct);

    public async Task<List<ServiceWorkflowStep>> GetWorkflowStepsByServiceAsync(Guid serviceId, CancellationToken ct = default)
        => await db.WorkflowSteps.Where(s => s.ServiceId == serviceId).OrderBy(s => s.StepOrder).ToListAsync(ct);

    // ── RequiredDocument ──────────────────────────────────────────────────
    public async Task AddRequiredDocumentAsync(ServiceRequiredDocument doc, CancellationToken ct = default)
        => await db.RequiredDocuments.AddAsync(doc, ct);

    // ── UoW ───────────────────────────────────────────────────────────────
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
