using Microsoft.EntityFrameworkCore;
using Sheba.Admin.Domain.Entities;
using Sheba.Shared.Kernel.Outbox;

namespace Sheba.Admin.Infrastructure.Persistence;

public class AdminDbContext : DbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options) { }

    public DbSet<DailyRegistrationSnapshot> DailyRegistrationSnapshots => Set<DailyRegistrationSnapshot>();
    public DbSet<DailyServiceRequestSnapshot> DailyServiceRequestSnapshots => Set<DailyServiceRequestSnapshot>();
    public DbSet<DailyRevenueSnapshot> DailyRevenueSnapshots => Set<DailyRevenueSnapshot>();
    public DbSet<ReportJob> ReportJobs => Set<ReportJob>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("admin_data");
        mb.ApplyConfigurationsFromAssembly(typeof(AdminDbContext).Assembly);
        mb.ApplyConfiguration(new OutboxMessageConfiguration());
        mb.ApplyConfiguration(new InboxMessageConfiguration());
        base.OnModelCreating(mb);
    }
}
