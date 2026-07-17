using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Sheba.Identity.Application.Commands.CreateRefreshTokenFamily;
using Sheba.Identity.Application.Commands.LoginAdmin;
using Sheba.Identity.Application.Commands.RotateRefreshTokenFamily;
using Sheba.Identity.Application.Commands.VerifyLoginOtp;
using Sheba.Shared.Kernel.RateLimiting;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sheba.Identity.Infrastructure.Oidc;

/// <summary>
/// OpenIddict token / userinfo / logout endpoint handlers.
///
/// These turn the OpenIddict server configuration (discovery, JWKS) into a working
/// OAuth 2.1 / OIDC provider by actually issuing signed tokens.
///
/// Grants supported at /connect/token:
///   • client_credentials              — ministries / machine-to-machine
///   • urn:sheba:grant:national_id_otp — citizen (accountId + verified login OTP → tokens)
///   • refresh_token                   — session extension with rotation
///
/// The custom citizen grant expects the citizen to have already completed
/// LoginCitizenCommand (password + OTP dispatch). The token request carries the
/// account_id and otp, which are re-validated here via VerifyLoginOtpCommand before
/// any token is minted. This keeps password/OTP logic in the Application layer.
/// </summary>
public static class OidcEndpoints
{
    public const string ShebaOtpGrantType = "urn:sheba:grant:national_id_otp";

    // Admin authentication is a distinct grant issuing a distinct principal (§10.1: AdminUser is
    // never the same principal as a citizen Account, even for the SuperAdmin who is also a
    // citizen). Admins who have completed TOTP enrollment (T-SEC-1) must also supply mfa_code —
    // enforced in LoginAdminHandler, not here, so the OIDC layer stays a thin transport.
    public const string ShebaAdminGrantType = "urn:sheba:grant:admin_password";

    public static WebApplication MapOidcEndpoints(this WebApplication app)
    {
        app.MapPost("/connect/token", HandleTokenAsync)
            .RequireRateLimiting(RateLimitPolicyNames.ConnectToken) // T-SEC-2
            .WithTags("OIDC");
        app.MapMethods("/connect/userinfo", new[] { "GET", "POST" }, HandleUserinfoAsync).WithTags("OIDC");
        app.MapMethods("/connect/logout", new[] { "GET", "POST" }, HandleLogoutAsync).WithTags("OIDC");
        return app;
    }

    // ── /connect/token ─────────────────────────────────────────────────────────
    private static async Task<IResult> HandleTokenAsync(HttpContext context, IMediator mediator)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict request cannot be retrieved.");

        if (request.IsClientCredentialsGrantType())
            return await IssueClientCredentialsAsync(request);

        if (request.GrantType == ShebaOtpGrantType)
            return await IssueCitizenTokenAsync(request, mediator);

        if (request.GrantType == ShebaAdminGrantType)
            return await IssueAdminTokenAsync(request, mediator);

        if (request.IsAuthorizationCodeGrantType())
            return await IssueFromAuthorizationCodeAsync(context);

        if (request.IsRefreshTokenGrantType())
            return await IssueFromRefreshTokenAsync(context, mediator);

        return TokenError(Errors.UnsupportedGrantType, "The specified grant type is not supported.");
    }

    // client_credentials — ministries / internal machine clients
    private static Task<IResult> IssueClientCredentialsAsync(OpenIddictRequest request)
    {
        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            Claims.Name, Claims.Role);

        identity.SetClaim(Claims.Subject, request.ClientId);
        identity.SetClaim(Claims.Name, request.ClientId);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        SetDestinationsForAll(principal);

        return Task.FromResult<IResult>(
            Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme));
    }

    // urn:sheba:grant:national_id_otp — citizen: account_id + verified login OTP → tokens
    private static async Task<IResult> IssueCitizenTokenAsync(OpenIddictRequest request, IMediator mediator)
    {
        var accountIdRaw = request.GetParameter("account_id")?.Value?.ToString();
        var otp = request.GetParameter("otp")?.Value?.ToString();

        if (!Guid.TryParse(accountIdRaw, out var accountId) || string.IsNullOrWhiteSpace(otp))
            return TokenError(Errors.InvalidRequest, "account_id and otp are required for this grant.");

        var result = await mediator.Send(new VerifyLoginOtpCommand(accountId, otp));
        if (result.IsFailure)
            return TokenError(Errors.InvalidGrant, result.Error!.Message);

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            Claims.Name, Claims.Role);

        identity.SetClaim(Claims.Subject, result.Value.AccountId.ToString());
        identity.SetClaim(Claims.Name, result.Value.FullNameEn);
        identity.SetClaim(Claims.PreferredUsername, result.Value.Username);
        identity.SetClaim(Claims.Email, result.Value.Email);
        identity.SetClaim("national_id_hash", HashNid(result.Value.NationalId));
        identity.SetClaim("loa", result.Value.IdentityLevel.ToString());
        // Every citizen token carries this fixed role. Authorization policies key off it to keep
        // citizen-only endpoints closed to admin principals and vice versa (§10.1).
        identity.SetClaim("role", "Citizen");

        var principal = new ClaimsPrincipal(identity);

        // Grant the standard OIDC scopes (plus offline_access for a refresh token). civil_data is
        // NOT defaulted in (T-OIDC-1) — a client must explicitly request it, and even then it's
        // only granted at LoA ≥ 2. The browser authorize+PKCE flow (AuthorizeEndpoints) is the
        // only path that additionally requires a recorded consent decision for it; this
        // first-party custom grant only gates on LoA, since sheba-portal is Sheba's own app, not
        // a third-party RP the citizen is being asked to trust with a share decision.
        var requested = request.GetScopes();
        var scopes = requested.Length > 0
            ? (IEnumerable<string>)requested
            : new[] { Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.OfflineAccess };

        if (scopes.Contains("civil_data") && result.Value.IdentityLevel < 2)
            return TokenError(Errors.InvalidScope, "civil_data requires Level of Assurance 2 or higher.");

        principal.SetScopes(scopes);
        SetDestinationsForAll(principal);
        await AttachRefreshFamilyClaimsAsync(identity, principal, result.Value.AccountId, request.ClientId, mediator);

        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // urn:sheba:grant:admin_password — admin: employee_id_or_email + password (+ mfa_code once
    // enrolled) → tokens. A separate principal type from the citizen grant above (§10.1) — the
    // role claim carries the AdminUser's specific sub-role (SuperAdmin, IdentityReviewer, ...),
    // which authorization policies match against.
    private static async Task<IResult> IssueAdminTokenAsync(OpenIddictRequest request, IMediator mediator)
    {
        var identifier = request.GetParameter("employee_id_or_email")?.Value?.ToString();
        var password = request.GetParameter("password")?.Value?.ToString();
        var mfaCode = request.GetParameter("mfa_code")?.Value?.ToString();

        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
            return TokenError(Errors.InvalidRequest, "employee_id_or_email and password are required for this grant.");

        var result = await mediator.Send(new LoginAdminCommand(identifier, password, mfaCode));
        if (result.IsFailure)
            return TokenError(Errors.InvalidGrant, result.Error!.Message);

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            Claims.Name, Claims.Role);

        identity.SetClaim(Claims.Subject, result.Value.AdminId.ToString());
        identity.SetClaim(Claims.Name, result.Value.FullName);
        identity.SetClaim(Claims.Email, result.Value.Email);
        identity.SetClaim("role", result.Value.Role);
        // T-AUTH-1: present only for MinistryManager. Ownership checks treat its absence as
        // "unrestricted" (SuperAdmin), never as "restricted to nothing" — see GetMinistryId.
        if (result.Value.MinistryId is { } ministryId)
            identity.SetClaim("ministry_id", ministryId.ToString());

        var principal = new ClaimsPrincipal(identity);

        var requested = request.GetScopes();
        var scopes = requested.Length > 0
            ? (IEnumerable<string>)requested
            : new[] { Scopes.OpenId, Scopes.Profile, "admin_api" };
        principal.SetScopes(scopes);
        SetDestinationsForAll(principal);
        await AttachRefreshFamilyClaimsAsync(identity, principal, result.Value.AdminId, request.ClientId, mediator);

        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // authorization_code — the browser PKCE flow (T-OIDC-1, AuthorizeEndpoints). OpenIddict has
    // already validated the code, PKCE code_verifier, and redirect_uri in its own pipeline before
    // this passthrough runs; this just re-signs the principal AuthorizeEndpoints originally
    // signed in during /connect/authorize.
    private static async Task<IResult> IssueFromAuthorizationCodeAsync(HttpContext context)
    {
        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        if (result.Principal is null)
            return TokenError(Errors.InvalidGrant, "The authorization code is no longer valid.");

        var principal = result.Principal;
        SetDestinationsForAll(principal);
        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // refresh_token — reuse the principal OpenIddict already validated; rotation is on by config.
    // Family reuse detection (T-SEC-9): a superseded refresh token being replayed doesn't just
    // fail this one request, it revokes the whole family (RFC 9700) — see RotateRefreshTokenFamilyHandler.
    private static async Task<IResult> IssueFromRefreshTokenAsync(HttpContext context, IMediator mediator)
    {
        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        if (result.Principal is null)
            return TokenError(Errors.InvalidGrant, "The refresh token is no longer valid.");

        var principal = result.Principal;

        var familyIdRaw = principal.GetClaim("family_id");
        var generationRaw = principal.GetClaim("family_generation");
        if (Guid.TryParse(familyIdRaw, out var familyId) && int.TryParse(generationRaw, out var presentedGeneration))
        {
            var rotation = await mediator.Send(new RotateRefreshTokenFamilyCommand(familyId, presentedGeneration));
            if (rotation.IsFailure)
                return TokenError(Errors.InvalidGrant, rotation.Error!.Message);

            var identity = (ClaimsIdentity)principal.Identity!;
            identity.SetClaim("family_generation", rotation.Value.ToString());
        }

        SetDestinationsForAll(principal);
        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // Attaches internal-only family_id/family_generation claims (no destination — see
    // SetDestinationsForAll's placement before this call — so they never leak into the JWT text)
    // when the response actually grants offline_access. OpenIddict restores the FULL principal
    // (every claim, regardless of destination) on every future refresh, which is how these two
    // claims survive across redemptions despite this endpoint never seeing the raw refresh token
    // value OpenIddict mints after it returns.
    private static async Task AttachRefreshFamilyClaimsAsync(
        ClaimsIdentity identity, ClaimsPrincipal principal, Guid subjectId, string? clientId, IMediator mediator)
    {
        if (!principal.HasScope(Scopes.OfflineAccess) || string.IsNullOrEmpty(clientId))
            return;

        var result = await mediator.Send(new CreateRefreshTokenFamilyCommand(subjectId, clientId));
        if (result.IsFailure)
            return; // best-effort bookkeeping — never block token issuance over it

        identity.SetClaim("family_id", result.Value.FamilyId.ToString());
        identity.SetClaim("family_generation", result.Value.Generation.ToString());
    }

    // ── /connect/userinfo ────────────────────────────────────────────────────
    private static async Task<IResult> HandleUserinfoAsync(HttpContext context)
    {
        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = result.Principal;
        if (principal is null)
            return Results.Unauthorized();

        var claims = new Dictionary<string, object?>
        {
            [Claims.Subject] = principal.GetClaim(Claims.Subject)
        };

        if (principal.HasScope(Scopes.Profile))
        {
            claims[Claims.Name]              = principal.GetClaim(Claims.Name);
            claims[Claims.PreferredUsername] = principal.GetClaim(Claims.PreferredUsername);
            claims["loa"]                    = principal.GetClaim("loa");
        }

        if (principal.HasScope(Scopes.Email))
            claims[Claims.Email] = principal.GetClaim(Claims.Email);

        if (principal.HasScope("civil_data"))
            claims["national_id_hash"] = principal.GetClaim("national_id_hash");

        return Results.Ok(claims);
    }

    // ── /connect/logout ───────────────────────────────────────────────────────
    private static IResult HandleLogoutAsync() =>
        Results.SignOut(
            authenticationSchemes: new[] { OpenIddictServerAspNetCoreDefaults.AuthenticationScheme },
            properties: new AuthenticationProperties { RedirectUri = "/" });

    // ── Helpers ────────────────────────────────────────────────────────────────
    private static IResult TokenError(string error, string description) =>
        Results.Forbid(
            authenticationSchemes: new[] { OpenIddictServerAspNetCoreDefaults.AuthenticationScheme },
            properties: new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
            }));

    // Every claim is emitted into both the access token and the id token for the demo.
    private static void SetDestinationsForAll(ClaimsPrincipal principal)
    {
        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(Destinations.AccessToken, Destinations.IdentityToken);
        }
    }

    private static string HashNid(string? nationalId)
    {
        if (string.IsNullOrEmpty(nationalId)) return string.Empty;
        var bytes = System.Text.Encoding.UTF8.GetBytes(nationalId);
        var hash  = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
