using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sheba.Citizen.Application.Commands.UpdateProfile;
using Sheba.Citizen.Application.Interfaces;
using Sheba.Citizen.Application.Queries.GetMyProfile;
using Sheba.Citizen.Infrastructure.Persistence;
using Sheba.Citizen.Infrastructure.Repositories;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Outbox;
using Sheba.Shared.Kernel.Persistence;
using Sheba.Shared.Kernel.Security;
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

    public static WebApplication MapCitizenEndpoints(this WebApplication app)
    {
        // Ownership is implicit: AccountId always comes from the caller's own token (sub claim),
        // never a route/body parameter — a citizen can only ever read/update their own profile.
        var citizens = app.MapGroup("/api/citizens").WithTags("Citizen — Profile")
            .RequireAuthorization("CitizenOnly")
            .AddEndpointFilter<Sheba.Shared.Kernel.Responses.JSendWrappingFilter>(); // JSend envelopes (T-API-1)

        citizens.MapGet("/me", async (
            ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetMyProfileQuery(user.RequireSubjectId()), ct);
            return Results.Ok(result);
        })
        .WithName("GetMyCitizenProfile")
        .WithSummary("Get the calling citizen's own profile.");

        citizens.MapPatch("/me", async (
            UpdateProfileBody body, ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
        {
            var command = new UpdateProfileCommand(
                user.RequireSubjectId(), body.Email, body.PhoneNumber, body.DateOfBirth,
                body.Address, body.City, body.Governorate);
            var result = await mediator.Send(command, ct);
            return Results.Ok(result);
        })
        .WithName("UpdateMyCitizenProfile")
        .WithSummary("Update optional fields on the calling citizen's own profile.");

        return app;
    }

    /// <summary>Request body for the profile update endpoint — AccountId is never client-supplied.</summary>
    public sealed record UpdateProfileBody(
        string? Email = null,
        string? PhoneNumber = null,
        DateOnly? DateOfBirth = null,
        string? Address = null,
        string? City = null,
        string? Governorate = null);
}
