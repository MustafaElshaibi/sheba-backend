using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sheba.Citizen.Application.Interfaces;
using Sheba.Citizen.Infrastructure.Persistence;
using Sheba.Citizen.Infrastructure.Repositories;
namespace Sheba.Citizen.Infrastructure;
public static class CitizenModule
{
    public static IServiceCollection AddCitizenModule(this IServiceCollection svc, IConfiguration cfg)
    {
        svc.AddDbContext<CitizenDbContext>(o => o.UseNpgsql(cfg.GetConnectionString("Default"), n => n.MigrationsHistoryTable("__ef_migrations", "citizen")));
        svc.AddScoped<ICitizenProfileRepository, CitizenProfileRepository>();
        return svc;
    }
    public static WebApplication MapCitizenEndpoints(this WebApplication app) { return app; }
}
