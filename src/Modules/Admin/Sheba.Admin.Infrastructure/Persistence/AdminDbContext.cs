using Microsoft.EntityFrameworkCore;
using Sheba.Admin.Domain.Entities;

namespace Sheba.Admin.Infrastructure.Persistence;

public class AdminDbContext : DbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options) { }

    public DbSet<DailyRegistrationSnapshot> DailyRegistrationSnapshots => Set<DailyRegistrationSnapshot>();
    public DbSet<DailyServiceRequestSnapshot> DailyServiceRequestSnapshots => Set<DailyServiceRequestSnapshot>();
    public DbSet<ReportJob> ReportJobs => Set<ReportJob>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("admin_data");
        mb.ApplyConfigurationsFromAssembly(typeof(AdminDbContext).Assembly);
        base.OnModelCreating(mb);
    }
}
