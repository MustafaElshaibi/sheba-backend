using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sheba.Identity.Infrastructure.Oidc;

/// <summary>
/// Relying Party (OAuth client / OIDC application) registry — admin API.
///
/// Section 5 lists a Relying Party registry as an Identity Service responsibility.
/// In this implementation OpenIddict IS the registry: its application store holds the
/// clients, redirect URIs, permissions and secrets. These admin endpoints are a thin,
/// safe management surface over IOpenIddictApplicationManager, so we do not duplicate
/// the OpenIddict tables with a parallel relying_parties schema.
///
/// Endpoints (admin):
///   GET    /api/admin/relying-parties            — list all registered clients
///   POST   /api/admin/relying-parties            — register a client (secret returned once)
///   GET    /api/admin/relying-parties/{clientId} — client detail (no secret)
///   DELETE /api/admin/relying-parties/{clientId} — revoke/remove a client
/// </summary>
public static class RelyingPartyEndpoints
{
    public static WebApplication MapRelyingPartyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/relying-parties").WithTags("Admin — Relying Parties");

        // ── List ─────────────────────────────────────────────────────────────
        group.MapGet("/", async (IOpenIddictApplicationManager manager, CancellationToken ct) =>
        {
            var items = new List<RelyingPartyDto>();
            await foreach (var app in manager.ListAsync(cancellationToken: ct))
            {
                items.Add(await ToDtoAsync(manager, app, ct));
            }
            return Results.Ok(items);
        })
        .WithName("ListRelyingParties")
        .WithSummary("List all registered relying parties (OIDC clients).");

        // ── Get by clientId ────────────────────────────────────────────────────
        group.MapGet("/{clientId}", async (
            string clientId, IOpenIddictApplicationManager manager, CancellationToken ct) =>
        {
            var app = await manager.FindByClientIdAsync(clientId, ct);
            return app is null
                ? Results.NotFound()
                : Results.Ok(await ToDtoAsync(manager, app, ct));
        })
        .WithName("GetRelyingParty")
        .WithSummary("Get a relying party by client_id (secret is never returned).");

        // ── Register ─────────────────────────────────────────────────────────
        group.MapPost("/", async (
            RegisterRelyingPartyRequest body, IOpenIddictApplicationManager manager, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.ClientId))
                return Results.BadRequest(new { error = "clientId is required." });

            if (await manager.FindByClientIdAsync(body.ClientId, ct) is not null)
                return Results.Conflict(new { error = $"A client with id '{body.ClientId}' already exists." });

            var isConfidential = string.Equals(body.ClientType, ClientTypes.Confidential, StringComparison.OrdinalIgnoreCase);

            // Confidential clients get a generated secret returned exactly once.
            string? generatedSecret = isConfidential
                ? (string.IsNullOrWhiteSpace(body.ClientSecret) ? Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N") : body.ClientSecret)
                : null;

            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId    = body.ClientId,
                DisplayName = body.DisplayName ?? body.ClientId,
                ClientType  = isConfidential ? ClientTypes.Confidential : ClientTypes.Public,
                ClientSecret = generatedSecret,
                Permissions =
                {
                    Permissions.Endpoints.Token,
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Logout,
                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.GrantTypes.RefreshToken,
                    Permissions.ResponseTypes.Code,
                }
            };

            foreach (var uri in body.RedirectUris ?? Array.Empty<string>())
                if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
                    descriptor.RedirectUris.Add(parsed);

            foreach (var uri in body.PostLogoutRedirectUris ?? Array.Empty<string>())
                if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
                    descriptor.PostLogoutRedirectUris.Add(parsed);

            foreach (var scope in body.Scopes ?? new[] { Scopes.OpenId, Scopes.Profile })
                descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);

            await manager.CreateAsync(descriptor, ct);

            return Results.Created($"/api/admin/relying-parties/{body.ClientId}", new
            {
                clientId = body.ClientId,
                clientType = descriptor.ClientType,
                // Returned ONLY here, at creation time. Never retrievable again.
                clientSecret = generatedSecret,
                message = isConfidential
                    ? "Store this client secret now — it will not be shown again."
                    : "Public client registered (PKCE required; no secret)."
            });
        })
        .WithName("RegisterRelyingParty")
        .WithSummary("Register a new relying party. For confidential clients the secret is returned once.");

        // ── Delete / revoke ────────────────────────────────────────────────────
        group.MapDelete("/{clientId}", async (
            string clientId, IOpenIddictApplicationManager manager, CancellationToken ct) =>
        {
            var app = await manager.FindByClientIdAsync(clientId, ct);
            if (app is null) return Results.NotFound();
            await manager.DeleteAsync(app, ct);
            return Results.NoContent();
        })
        .WithName("DeleteRelyingParty")
        .WithSummary("Revoke and remove a relying party.");

        return app;
    }

    private static async Task<RelyingPartyDto> ToDtoAsync(
        IOpenIddictApplicationManager manager, object app, CancellationToken ct)
    {
        var clientId    = await manager.GetClientIdAsync(app, ct);
        var displayName = await manager.GetDisplayNameAsync(app, ct);
        var clientType  = await manager.GetClientTypeAsync(app, ct);
        var redirects   = await manager.GetRedirectUrisAsync(app, ct);
        var permissions = await manager.GetPermissionsAsync(app, ct);

        var scopes = permissions
            .Where(p => p.StartsWith(Permissions.Prefixes.Scope, StringComparison.Ordinal))
            .Select(p => p[Permissions.Prefixes.Scope.Length..])
            .ToArray();

        return new RelyingPartyDto(
            ClientId:    clientId ?? "",
            DisplayName: displayName,
            ClientType:  clientType,
            RedirectUris: redirects.ToArray(),
            Scopes:      scopes);
    }
}

public sealed record RegisterRelyingPartyRequest(
    string ClientId,
    string? DisplayName,
    string? ClientType,                 // "public" (default) or "confidential"
    string? ClientSecret,               // optional; generated if confidential and omitted
    string[]? RedirectUris,
    string[]? PostLogoutRedirectUris,
    string[]? Scopes);

public sealed record RelyingPartyDto(
    string ClientId,
    string? DisplayName,
    string? ClientType,
    string[] RedirectUris,
    string[] Scopes);
