using System.Security.Claims;

namespace Sheba.Shared.Kernel.Security;

/// <summary>
/// Helpers for reading the authenticated principal. The golden rule these support: the acting
/// subject (which citizen, which admin) is taken from the verified token — never from a request
/// body or route parameter the caller controls. Reading an id from the payload lets any
/// authenticated caller act as anyone else; reading it from <c>sub</c> does not.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The subject id (<c>sub</c>) of the token. For a citizen this is their account id; for an
    /// admin it is their <c>AdminUser</c> id. Returns null when unauthenticated or malformed.
    /// </summary>
    public static Guid? GetSubjectId(this ClaimsPrincipal user)
    {
        // OpenIddict emits "sub"; some middleware remaps it to NameIdentifier — accept either.
        var sub = user.FindFirst("sub")?.Value
                  ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    /// <summary>Same as <see cref="GetSubjectId"/> but throws when absent — use in endpoints that
    /// are already behind <c>RequireAuthorization</c>, where a missing subject is a bug, not input.</summary>
    public static Guid RequireSubjectId(this ClaimsPrincipal user) =>
        user.GetSubjectId()
        ?? throw new InvalidOperationException("Authenticated principal has no usable subject id.");

    /// <summary>The role claim value, accepting either the "role" or the mapped role claim type.</summary>
    public static string? GetRole(this ClaimsPrincipal user) =>
        user.FindFirst("role")?.Value ?? user.FindFirst(ClaimTypes.Role)?.Value;

    /// <summary>
    /// The admin's ministry scope (T-AUTH-1) — present only for MinistryManager tokens. Null
    /// means either not an admin token, or an unrestricted role (SuperAdmin sees every ministry).
    /// Callers enforcing ownership treat null as "no restriction", never as "restricted to
    /// nothing" — the absence of a claim is not itself a denial.
    /// </summary>
    public static Guid? GetMinistryId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("ministry_id")?.Value, out var id) ? id : null;
}
