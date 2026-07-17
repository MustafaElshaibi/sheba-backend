using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sheba.Audit.Infrastructure.Behaviors;

/// <summary>
/// Renders a command/response object as a JSON audit snapshot with a redaction *allowlist*
/// (T-AUD-4): only property names in <see cref="SafeFieldNames"/> are written verbatim; every
/// other property — known-sensitive (passwords, OTP/TOTP codes, national IDs, phone numbers,
/// tokens/secrets) and anything not yet reviewed — is replaced with a placeholder.
///
/// Default-deny on purpose, per the repo's no-PII-logging rule (docs/coding-standards.md §7 —
/// "log entity IDs instead"): a brand-new command field that nobody has added to the allowlist
/// yet is redacted, not leaked, until someone deliberately opts it in.
/// </summary>
public static class AuditSnapshotRedactor
{
    private const string RedactedPlaceholder = "[REDACTED]";
    private const int MaxSnapshotLength = 4096;

    private static readonly HashSet<string> SafeFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Entity/aggregate ids — the "log entity IDs instead" default.
        "Id", "EntityId", "AccountId", "AdminId", "CitizenId", "MinistryId", "ServiceId",
        "RequestId", "IdentityRequestId", "DocumentId", "CredentialId", "EndpointId", "FeeId",
        "WebhookId", "AuthConfigId", "CategoryId", "ParentId", "ParentMinistryId", "ProfileId",
        "OwnerId", "ReviewedByAdminId", "DeliveryId", "OidcClientId",
        // Sheba.Shared.Kernel.Results.Result<T>/Error wrapper shape (T-STD-1 modules) — these are
        // structural envelope keys, not data, so they're safe to keep and recurse into; the
        // interesting fields inside "value" still go through the same allowlist.
        "Value", "IsSuccess", "IsFailure", "Error",
        // Structural / non-secret scalars.
        "Accepted", "ActorId", "Amount", "ApiKeyHeaderName", "ApiKeyPlacementType", "AuthType",
        "AverageDays", "BaseUrl", "City", "Code", "Completed", "ContactEmail", "Currency",
        "CurrentStep", "Deleted", "DescriptionAr", "DescriptionEn", "DisplayOrder", "DocumentType",
        "Error", "EventType", "FeeType", "FormSchemaJson", "GatewayReference", "Governorate",
        "HealthCheckPath", "HttpMethod", "IconUrl", "IdentityLevel", "IsActive", "IsAdmin",
        "IsDefault", "IsMandatory", "IsOnline", "LatencyMs", "LogoUrl", "MaskedPhone", "Message",
        "Name", "NameAr", "NameEn", "OidcScope", "OidcTokenEndpoint", "PathTemplate", "Priority",
        "Publish", "RateLimitPerMinute", "ReferenceNumber", "RejectionReason", "RequiredLoa",
        "RequiresAppointment", "RequiresCitizenConsent", "RetryCount", "Role", "ShebaWebhookPath",
        "SizeBytes", "Status", "StatusCode", "Success", "TargetLevel", "TimeoutSeconds",
        "Timestamp", "Type", "UiSchemaJson", "Username", "WebsiteUrl", "AddressAr", "AddressEn"
    };

    public static string? Redact(object? value, JsonSerializerOptions options)
    {
        if (value is null) return null;

        try
        {
            var node = JsonSerializer.SerializeToNode(value, value.GetType(), options);
            RedactNode(node);

            var json = node?.ToJsonString(options) ?? "null";
            return json.Length > MaxSnapshotLength ? json[..MaxSnapshotLength] + "..." : json;
        }
        catch
        {
            return $$"""{"_error":"Serialization failed for {{value.GetType().Name}}"}""";
        }
    }

    private static void RedactNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(kvp => kvp.Key).ToList())
                {
                    if (SafeFieldNames.Contains(key))
                        RedactNode(obj[key]);
                    else
                        obj[key] = RedactedPlaceholder;
                }
                break;

            case JsonArray arr:
                foreach (var item in arr)
                    RedactNode(item);
                break;
        }
    }
}
