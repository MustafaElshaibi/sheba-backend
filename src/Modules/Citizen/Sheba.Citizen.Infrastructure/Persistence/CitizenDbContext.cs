using Microsoft.EntityFrameworkCore;
using Sheba.Citizen.Domain.Entities;

namespace Sheba.Citizen.Infrastructure.Persistence;

/// <summary>
/// Citizen schema DbContext. Manages CitizenProfile entities.
/// </summary>
public sealed class CitizenDbContext : DbContext
{
    public DbSet<CitizenProfile> CitizenProfiles => Set<CitizenProfile>();

    public CitizenDbContext(DbContextOptions<CitizenDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("citizen");

        mb.Entity<CitizenProfile>(e =>
        {
            e.ToTable("citizen_profiles");
            e.HasKey(x => x.Id);

            e.Property(x => x.AccountId).IsRequired();
            e.Property(x => x.NationalId).HasMaxLength(20).IsRequired();
            e.Property(x => x.FullNameAr).HasMaxLength(200).IsRequired();
            e.Property(x => x.FullNameEn).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.PhoneNumber).HasMaxLength(20);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.Governorate).HasMaxLength(100);

            e.HasIndex(x => x.AccountId).IsUnique();
            e.HasIndex(x => x.NationalId).IsUnique();
        });

        base.OnModelCreating(mb);
    }
}
