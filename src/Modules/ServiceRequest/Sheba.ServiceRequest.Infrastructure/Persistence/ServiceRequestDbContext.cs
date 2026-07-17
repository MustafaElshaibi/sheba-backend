using Microsoft.EntityFrameworkCore;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.Shared.Kernel.Outbox;

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
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("service_req");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ServiceRequestDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
