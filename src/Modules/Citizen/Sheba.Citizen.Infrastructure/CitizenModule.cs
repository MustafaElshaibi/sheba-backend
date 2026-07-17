using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sheba.Citizen.Application.Interfaces;
using Sheba.Citizen.Infrastructure.Persistence;
using Sheba.Citizen.Infrastructure.Repositories;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Outbox;
using Sheba.Shared.Kernel.Persistence;
namespace Sheba.Citizen.Infrastructure;
public static class CitizenModule
{
    public static IServiceCollection AddCitizenModule(this IServiceCollection svc, IConfiguration cfg)
    {
        svc.AddDbContext<CitizenDbContext>(o => o.UseNpgsql(cfg.GetConnectionString("Default"), n => n.MigrationsHistoryTable("__ef_migrations", "citizen")).AddInterceptors(new OutboxSaveChangesInterceptor()));
        // Expose as base DbContext so the startup migration runner discovers this context (T-DB-1).
        svc.AddScoped<DbContext>(sp => sp.GetRequiredService<CitizenDbContext>());
        svc.AddScoped<ICitizenProfileRepository, CitizenProfileRepository>();
        svc.AddScoped<IUnitOfWork, EfUnitOfWork<CitizenDbContext>>();
        svc.AddScoped<IInboxGuard, EfInboxGuard<CitizenDbContext>>();
        return svc;
    }
    public static WebApplication MapCitizenEndpoints(this WebApplication app) { return app; }
}
