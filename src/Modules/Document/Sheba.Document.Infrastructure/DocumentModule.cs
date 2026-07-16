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
        });

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<DocumentDbContext>());
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddSingleton<IFileStorage, MinioFileStorage>();

        return services;
    }

    public static WebApplication MapDocumentEndpoints(this WebApplication app)
    {
        var docs = app.MapGroup("/api/documents").WithTags("Documents");

        // ── Upload (multipart/form-data) ──────────────────────────────────────
        docs.MapPost("/", async (
            IFormFile file, Guid ownerId, string? documentType,
            IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new UploadDocumentCommand(ownerId, file, documentType ?? "GENERAL"), ct);
            return Results.Created($"/api/documents/{result.DocumentId}", result);
        })
        .DisableAntiforgery()
        .WithName("UploadDocument")
        .WithSummary("Upload a document (JPEG/PNG/WebP/PDF, max 10 MB) to MinIO storage.");

        // ── List my documents ─────────────────────────────────────────────────
        docs.MapGet("/mine/{ownerId:guid}", async (
            Guid ownerId, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetMyDocumentsQuery(ownerId), ct);
            return Results.Ok(result);
        })
        .WithName("GetMyDocuments")
        .WithSummary("List all documents owned by a citizen.");

        // ── Presigned download URL ────────────────────────────────────────────
        docs.MapGet("/{id:guid}/download-url", async (
            Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetDocumentDownloadUrlQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetDocumentDownloadUrl")
        .WithSummary("Generate a presigned MinIO download URL (valid 15 minutes).");

        // ── Delete ────────────────────────────────────────────────────────────
        docs.MapDelete("/{id:guid}", async (
            Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeleteDocumentCommand(id), ct);
            return Results.Ok(result);
        })
        .WithName("DeleteDocument")
        .WithSummary("Delete a document from MinIO and mark metadata deleted.");

        return app;
    }
}
