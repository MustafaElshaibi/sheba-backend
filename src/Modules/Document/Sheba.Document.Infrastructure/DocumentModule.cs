using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sheba.Document.Application.Commands.DeleteDocument;
using Sheba.Document.Application.Commands.UploadDocument;
using Sheba.Document.Application.Queries.GetDocumentDownloadUrl;
using Sheba.Document.Application.Queries.GetMyDocuments;
using Sheba.Document.Domain.Interfaces;
using Sheba.Document.Infrastructure.Persistence;
using Sheba.Document.Infrastructure.Persistence.Repositories;
using Sheba.Document.Infrastructure.Storage;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Outbox;
using Sheba.Shared.Kernel.Persistence;
using Sheba.Shared.Kernel.Security;

namespace Sheba.Document.Infrastructure;

public static class DocumentModule
{
    public static IServiceCollection AddDocumentModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DocumentDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__ef_migrations", "document");
                    npgsql.MigrationsAssembly(typeof(DocumentModule).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3);
                });
            options.AddInterceptors(new OutboxSaveChangesInterceptor());
        });

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<DocumentDbContext>());
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddSingleton<IFileStorage, MinioFileStorage>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork<DocumentDbContext>>();
        services.AddScoped<IInboxGuard, EfInboxGuard<DocumentDbContext>>();

        return services;
    }

    public static WebApplication MapDocumentEndpoints(this WebApplication app)
    {
        // Every operation here acts on a citizen-owned resource — the whole group requires an
        // authenticated principal (T-AUTH-2); ownership itself comes from the token "sub", never
        // a caller-supplied id, and is enforced per-endpoint below (BR-DO-1).
        var docs = app.MapGroup("/api/documents").WithTags("Documents")
            .RequireAuthorization()
            .AddEndpointFilter<Sheba.Shared.Kernel.Responses.JSendWrappingFilter>(); // JSend envelopes (T-API-1)

        // ── Upload (multipart/form-data) ──────────────────────────────────────
        docs.MapPost("/", async (
            IFormFile file, string? documentType, System.Security.Claims.ClaimsPrincipal user,
            IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new UploadDocumentCommand(user.RequireSubjectId(), file, documentType ?? "GENERAL"), ct);
            return Results.Created($"/api/documents/{result.DocumentId}", result);
        })
        .DisableAntiforgery()
        .WithName("UploadDocument")
        .WithSummary("Upload a document (JPEG/PNG/WebP/PDF, max 10 MB) to MinIO storage, owned by the caller.");

        // ── List my documents ─────────────────────────────────────────────────
        docs.MapGet("/mine", async (
            System.Security.Claims.ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetMyDocumentsQuery(user.RequireSubjectId()), ct);
            return Results.Ok(result);
        })
        .WithName("GetMyDocuments")
        .WithSummary("List all documents owned by the calling citizen.");

        // ── Presigned download URL ────────────────────────────────────────────
        docs.MapGet("/{id:guid}/download-url", async (
            Guid id, System.Security.Claims.ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
        {
            // Actor identity comes from the token — the handler enforces BR-DO-1 (owners only,
            // unless admin) and returns null for non-owners so document existence never leaks.
            var isAdmin = user.GetRole() != "Citizen";
            var result = await mediator.Send(
                new GetDocumentDownloadUrlQuery(id, user.RequireSubjectId(), isAdmin), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetDocumentDownloadUrl")
        .WithSummary("Generate a presigned MinIO download URL (valid 15 minutes).");

        // ── Delete ────────────────────────────────────────────────────────────
        docs.MapDelete("/{id:guid}", async (
            Guid id, System.Security.Claims.ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
        {
            var isAdmin = user.GetRole() != "Citizen";
            var result = await mediator.Send(
                new DeleteDocumentCommand(id, user.RequireSubjectId(), isAdmin), ct);
            return Results.Ok(result);
        })
        .WithName("DeleteDocument")
        .WithSummary("Delete a document (owner or admin only) from MinIO and mark metadata deleted.");

        return app;
    }
}
