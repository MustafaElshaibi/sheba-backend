using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using StackExchange.Redis;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sheba.Identity.Infrastructure.Oidc;

/// <summary>
/// Browser-based authorization-code + PKCE flow (T-OIDC-1) — the "Sign in with Sheba" SSO path
/// external RPs use via a top-level redirect, distinct from the JSON/API-only custom grants in
/// OidcEndpoints.cs. PKCE itself needs no code here: RequireProofKeyForCodeExchange() (already
/// configured in IdentityModule.cs) makes OpenIddict validate code_challenge/code_verifier in its
/// own pipeline before and after this passthrough runs.
///
/// Flow:
///   1. GET /connect/authorize — checks the Sheba session cookie (SheebaSessionScheme), set by
///      POST /api/identity/session/establish after a citizen completes the existing password+OTP
///      login and holds a bearer token. Not authenticated → redirect to the portal's login page
///      with a return_url. civil_data requested → LoA ≥ 2 gate, then a consent redirect unless a
///      short-lived Redis marker shows consent was just granted (step 3). Otherwise issues the
///      code directly via the same claims already proven for the cookie session.
///   2. GET /connect/consent — minimal server-rendered consent prompt. No separate frontend
///      surface exists for this yet (see docs/sheba.md §6.10) — this is the one place that
///      genuinely needs interactive UI with nothing else in the system to redirect to for it.
///      Deliberately NOT persisted across sessions (no "remember consent forever" store): a
///      simpler, lower-risk scope than wiring OpenIddict's authorization-manager store, and the
///      AC only requires consent to gate the grant, not that it survive future logins.
///   3. POST /connect/consent/decide — Allow sets a 2-minute one-time Redis marker and redirects
///      back to step 1; Deny redirects to the RP's redirect_uri with error=access_denied
///      (RFC 6749 §4.1.2.1) — never back through /connect/authorize, so a denied consent can't be
///      retried by simply reloading.
/// </summary>
public static class AuthorizeEndpoints
{
    public const string SheebaSessionScheme = "SheebaSession";
    private static readonly TimeSpan ConsentMarkerTtl = TimeSpan.FromMinutes(2);

    public static WebApplication MapAuthorizeEndpoints(this WebApplication app)
    {
        app.MapGet("/connect/authorize", HandleAuthorizeAsync).WithTags("OIDC");
        app.MapGet("/connect/consent", HandleConsentPromptAsync).WithTags("OIDC");
        app.MapPost("/connect/consent/decide", HandleConsentDecisionAsync).WithTags("OIDC");
        return app;
    }

    private static async Task<IResult> HandleAuthorizeAsync(
        HttpContext context, IConnectionMultiplexer redis, IConfiguration configuration)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict authorization request cannot be retrieved.");

        var auth = await context.AuthenticateAsync(SheebaSessionScheme);
        if (auth.Principal is null)
        {
            var loginUrl = configuration["Identity:PortalLoginUrl"] ?? "https://localhost:4200/login";
            var returnUrl = UrlEncoder.Default.Encode(context.Request.GetEncodedUrl());
            return Results.Redirect($"{loginUrl}?return_url={returnUrl}");
        }

        var requestedScopes = request.GetScopes();
        var subjectId = auth.Principal.FindFirst(Claims.Subject)?.Value ?? string.Empty;

        if (requestedScopes.Contains("civil_data"))
        {
            var loaRaw = auth.Principal.FindFirst("loa")?.Value;
            var loa = int.TryParse(loaRaw, out var parsedLoa) ? parsedLoa : 1;
            if (loa < 2)
                return ForbidToClient(Errors.InvalidScope, "civil_data requires Level of Assurance 2 or higher.");

            var db = redis.GetDatabase();
            var marker = ConsentMarkerKey(subjectId, request.ClientId!, "civil_data");
            var justConsented = await db.KeyDeleteAsync(marker); // one-time use

            if (!justConsented)
                return Results.Redirect($"/connect/consent{context.Request.QueryString}");
        }

        return IssueAuthorizationCode(auth.Principal, requestedScopes);
    }

    private static async Task<IResult> HandleConsentPromptAsync(HttpContext context)
    {
        var auth = await context.AuthenticateAsync(SheebaSessionScheme);
        if (auth.Principal is null)
            return Results.Redirect($"/connect/authorize{context.Request.QueryString}"); // re-enter from the top

        var clientId = context.Request.Query["client_id"].ToString();
        var html = ConsentPageHtml(clientId, context.Request.QueryString.Value ?? string.Empty);
        return Results.Content(html, "text/html");
    }

    private static async Task<IResult> HandleConsentDecisionAsync(HttpContext context, IConnectionMultiplexer redis)
    {
        var form = await context.Request.ReadFormAsync();
        var originalQuery = form["query"].ToString();
        var decision = form["decision"].ToString();
        var parsed = QueryHelpers.ParseQuery(originalQuery.TrimStart('?'));

        var clientId = parsed.TryGetValue("client_id", out var cid) ? cid.ToString() : string.Empty;
        var redirectUri = parsed.TryGetValue("redirect_uri", out var ru) ? ru.ToString() : string.Empty;
        var state = parsed.TryGetValue("state", out var st) ? st.ToString() : null;

        if (!string.Equals(decision, "allow", StringComparison.OrdinalIgnoreCase))
        {
            var stateQuery = state is null ? string.Empty : $"&state={UrlEncoder.Default.Encode(state)}";
            return Results.Redirect(
                $"{redirectUri}?error=access_denied&error_description=The+citizen+declined+the+consent+request.{stateQuery}");
        }

        var auth = await context.AuthenticateAsync(SheebaSessionScheme);
        if (auth.Principal is null)
            return Results.Redirect($"/connect/authorize{originalQuery}");

        var subjectId = auth.Principal.FindFirst(Claims.Subject)?.Value ?? string.Empty;
        var db = redis.GetDatabase();
        await db.StringSetAsync(ConsentMarkerKey(subjectId, clientId, "civil_data"), "granted", ConsentMarkerTtl);

        return Results.Redirect($"/connect/authorize{originalQuery}");
    }

    // Reuses the claims already proven for the cookie session — no re-validation of who the
    // subject is, only which scopes this specific grant carries.
    private static IResult IssueAuthorizationCode(ClaimsPrincipal cookiePrincipal, IEnumerable<string> requestedScopes)
    {
        var identity = new ClaimsIdentity(
            cookiePrincipal.Claims,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            Claims.Name, Claims.Role);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(requestedScopes);

        foreach (var claim in principal.Claims)
            claim.SetDestinations(Destinations.AccessToken, Destinations.IdentityToken);

        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static IResult ForbidToClient(string error, string description) =>
        Results.Forbid(
            authenticationSchemes: new[] { OpenIddictServerAspNetCoreDefaults.AuthenticationScheme },
            properties: new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
            }));

    private static string ConsentMarkerKey(string subjectId, string clientId, string scope) =>
        $"sheba:consent:{subjectId}:{clientId}:{scope}";

    private static string ConsentPageHtml(string clientId, string queryString)
    {
        var safeClientId = HtmlEscape(clientId);
        var safeQuery = HtmlEscape(queryString);

        var sb = new System.Text.StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<title>Sheba — Authorize ").Append(safeClientId).Append("</title></head>");
        sb.Append("<body style=\"font-family: sans-serif; max-width: 480px; margin: 4rem auto; line-height: 1.5;\">");
        sb.Append("<h2>Allow ").Append(safeClientId).Append(" to access your Sheba identity?</h2>");
        sb.Append("<p>This app is asking to view your verified identity claims (a one-way hash of ");
        sb.Append("your national ID, your name, and your verified assurance level). It will not see ");
        sb.Append("your raw national ID number.</p>");
        sb.Append("<form method=\"post\" action=\"/connect/consent/decide\">");
        sb.Append("<input type=\"hidden\" name=\"query\" value=\"").Append(safeQuery).Append("\" />");
        sb.Append("<button type=\"submit\" name=\"decision\" value=\"allow\">Allow</button>");
        sb.Append("<button type=\"submit\" name=\"decision\" value=\"deny\">Deny</button>");
        sb.Append("</form></body></html>");
        return sb.ToString();
    }

    private static string HtmlEscape(string value) => System.Net.WebUtility.HtmlEncode(value);
}
