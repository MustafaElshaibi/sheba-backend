using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Sheba.Identity.Application.Commands.VerifyLoginOtp;
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

    public static WebApplication MapOidcEndpoints(this WebApplication app)
    {
        app.MapPost("/connect/token", HandleTokenAsync).WithTags("OIDC");
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

        if (request.IsRefreshTokenGrantType())
            return IssueFromRefreshToken(context);

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
        if (!result.Succeeded)
            return TokenError(Errors.InvalidGrant, result.Message ?? "Login verification failed.");

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            Claims.Name, Claims.Role);

        identity.SetClaim(Claims.Subject, result.AccountId.ToString());
        identity.SetClaim(Claims.Name, result.FullNameEn);
        identity.SetClaim(Claims.PreferredUsername, result.Username);
        identity.SetClaim(Claims.Email, result.Email);
        identity.SetClaim("national_id_hash", HashNid(result.NationalId));
        identity.SetClaim("loa", result.IdentityLevel.ToString());

        var principal = new ClaimsPrincipal(identity);

        // Grant the standard OIDC scopes (plus offline_access for a refresh token).
        var requested = request.GetScopes();
        var scopes = requested.Length > 0
            ? (IEnumerable<string>)requested
            : new[] { Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.OfflineAccess, "civil_data" };
        principal.SetScopes(scopes);
        SetDestinationsForAll(principal);

        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // refresh_token — reuse the principal OpenIddict already validated; rotation is on by config
    private static IResult IssueFromRefreshToken(HttpContext context)
    {
        var result = context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
            .GetAwaiter().GetResult();

        if (result.Principal is null)
            return TokenError(Errors.InvalidGrant, "The refresh token is no longer valid.");

        var principal = result.Principal;
        SetDestinationsForAll(principal);
        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
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
