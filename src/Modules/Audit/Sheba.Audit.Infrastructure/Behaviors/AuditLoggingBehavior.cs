using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Sheba.Audit.Application.Interfaces;
using Sheba.Audit.Domain.Entities;

namespace Sheba.Audit.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior that writes an AuditEvent after every command executes.
///
/// Filters:
///   - Only intercepts commands (type name ends with "Command")
///   - Skips queries (type name ends with "Query") for performance
///
/// Captures:
///   - Actor ID from JWT "sub" claim
///   - IP address from HttpContext
///   - Redacted JSON snapshot of the request payload (see <see cref="AuditSnapshotRedactor"/> —
///     passwords, OTP/TOTP codes, national IDs, phone numbers, and tokens never reach the row)
///   - Redacted JSON snapshot of the response (on success)
///   - Error message (on failure)
///
/// Registered in Program.cs as the 4th pipeline behavior (after Transaction).
/// </summary>
public sealed class AuditLoggingBehavior<TRequest, TResponse>(
    IAuditRepository auditRepo,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuditLoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly JsonSerializerOptions SnapshotOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        MaxDepth = 5
    };

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        // Only audit commands, not queries
        if (!requestName.EndsWith("Command", StringComparison.OrdinalIgnoreCase))
            return await next();

        var actorId = GetActorId();
        var ipAddress = GetIpAddress();
        var requestSnapshot = SafeSerialize(request);

        try
        {
            var response = await next();

            var responseSnapshot = SafeSerialize(response);

            // Try to extract entity type and ID from the response
            var (entityType, entityId) = ExtractEntityInfo(request, response);

            var auditEvent = AuditEvent.CreateSuccess(
                actorId, requestName, entityType, entityId,
                ipAddress, requestSnapshot, responseSnapshot);

            await auditRepo.AddAsync(auditEvent, cancellationToken);
            await auditRepo.SaveChangesAsync(cancellationToken);

            logger.LogDebug("[Audit] {Action} by {ActorId} on {EntityType}/{EntityId} — OK",
                requestName, actorId, entityType, entityId);

            return response;
        }
        catch (Exception ex)
        {
            try
            {
                var auditEvent = AuditEvent.CreateFailure(
                    actorId, requestName, null, null,
                    ipAddress, requestSnapshot, ex.Message);

                await auditRepo.AddAsync(auditEvent, cancellationToken);
                await auditRepo.SaveChangesAsync(cancellationToken);
            }
            catch (Exception auditEx)
            {
                // Never let audit logging failures mask the original error
                logger.LogWarning(auditEx, "[Audit] Failed to write audit event for {Action}", requestName);
            }

            throw; // Re-throw the original exception
        }
    }

    private Guid GetActorId()
    {
        var sub = httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
                ?? httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    private string? GetIpAddress()
    {
        return httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>Redacted, size-capped JSON snapshot — see <see cref="AuditSnapshotRedactor"/>.</summary>
    private static string? SafeSerialize(object? value) =>
        AuditSnapshotRedactor.Redact(value, SnapshotOptions);

    /// <summary>
    /// Attempts to extract entity type and ID from request/response via reflection.
    /// Looks for common property names: EntityId, Id, AccountId, RequestId.
    /// </summary>
    private static (string? EntityType, Guid? EntityId) ExtractEntityInfo(TRequest request, TResponse? response)
    {
        // Try to infer entity type from command name (e.g. "ApproveIdentityRequestCommand" → "IdentityRequest")
        var requestName = typeof(TRequest).Name;
        var entityType = requestName
            .Replace("Command", "")
            .Replace("Create", "").Replace("Update", "").Replace("Delete", "")
            .Replace("Approve", "").Replace("Reject", "").Replace("Submit", "");

        if (string.IsNullOrWhiteSpace(entityType))
            entityType = null;

        // Try to extract an entity ID from the response
        Guid? entityId = null;
        if (response is not null)
        {
            entityId = TryGetGuidProperty(response, "Id")
                    ?? TryGetGuidProperty(response, "EntityId")
                    ?? TryGetGuidProperty(response, "AccountId")
                    ?? TryGetGuidProperty(response, "RequestId");
        }

        // Fallback: try the request itself
        entityId ??= TryGetGuidProperty(request, "Id")
                   ?? TryGetGuidProperty(request, "RequestId")
                   ?? TryGetGuidProperty(request, "AccountId");

        return (entityType, entityId);
    }

    private static Guid? TryGetGuidProperty(object obj, string propertyName)
    {
        var prop = obj.GetType().GetProperty(propertyName);
        if (prop is null) return null;

        var value = prop.GetValue(obj);
        return value switch
        {
            Guid g => g == Guid.Empty ? null : g,
            string s when Guid.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }
}
