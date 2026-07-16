using Microsoft.EntityFrameworkCore;
using Sheba.ServiceRequest.Domain.Entities;

namespace Sheba.ServiceRequest.Infrastructure.Persistence;

public sealed class ServiceRequestDbContext(DbContextOptions<ServiceRequestDbContext> options)
    : DbContext(options)
{
    public DbSet<ServiceCategory> Categories => Set<ServiceCategory>();
    public DbSet<ServiceDefinition> Services => Set<ServiceDefinition>();
    public DbSet<ServiceFormSchema> FormSchemas => Set<ServiceFormSchema>();
    public DbSet<ServiceFee> Fees => Set<ServiceFee>();
    public DbSet<ServiceRequiredDocument> RequiredDocuments => Set<ServiceRequiredDocument>();
    public DbSet<ServiceWorkflowStep> WorkflowSteps => Set<ServiceWorkflowStep>();
    public DbSet<ServiceRequestEntity> ServiceRequests => Set<ServiceRequestEntity>();
    public DbSet<RequestStepExecution> StepExecutions => Set<RequestStepExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("service_req");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ServiceRequestDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
