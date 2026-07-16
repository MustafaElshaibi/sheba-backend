using Sheba.Shared.Kernel.Entities;

namespace Sheba.Audit.Domain.Entities;

/// <summary>
/// Append-only audit log entry.
/// Captures who did what, to which entity, when, from where, and what changed.
/// The audit schema has only INSERT permission for the app DB user — no UPDATE or DELETE.
/// </summary>
public sealed class AuditEvent : BaseEntity
{
    /// <summary>Authenticated user's account ID (from JWT "sub" claim). Guid.Empty for anonymous/system.</summary>
    public Guid ActorId { get; private set; }

    /// <summary>The MediatR command name, e.g. "RegisterCitizenCommand".</summary>
    public string Action { get; private set; } = default!;

    /// <summary>The primary entity type affected, e.g. "Account", "IdentityRequest".</summary>
    public string? EntityType { get; private set; }

    /// <summary>The primary entity ID affected.</summary>
    public Guid? EntityId { get; private set; }

    /// <summary>UTC timestamp of the action.</summary>
    public DateTime Timestamp { get; private set; }

    /// <summary>Client IP address (from HttpContext.Connection.RemoteIpAddress).</summary>
    public string? IpAddress { get; private set; }

    /// <summary>JSON snapshot of the request (command) payload — the "before" or input state.</summary>
    public string? RequestSnapshot { get; private set; }

    /// <summary>JSON snapshot of the handler response — the "after" or output state.</summary>
    public string? ResponseSnapshot { get; private set; }

    /// <summary>Whether the command completed successfully.</summary>
    public bool Succeeded { get; private set; }

    /// <summary>Error message if the command failed.</summary>
    public string? ErrorMessage { get; private set; }

    private AuditEvent() { }

    /// <summary>Creates a successful audit event.</summary>
    public static AuditEvent CreateSuccess(
        Guid actorId,
        string action,
        string? entityType,
        Guid? entityId,
        string? ipAddress,
        string? requestSnapshot,
        string? responseSnapshot)
    {
        return new AuditEvent
        {
            Id = Guid.NewGuid(),
            ActorId = actorId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Timestamp = DateTime.UtcNow,
            IpAddress = ipAddress,
            RequestSnapshot = requestSnapshot,
            ResponseSnapshot = responseSnapshot,
            Succeeded = true
        };
    }

    /// <summary>Creates a failed audit event.</summary>
    public static AuditEvent CreateFailure(
        Guid actorId,
        string action,
        string? entityType,
        Guid? entityId,
        string? ipAddress,
        string? requestSnapshot,
        string errorMessage)
    {
        return new AuditEvent
        {
            Id = Guid.NewGuid(),
            ActorId = actorId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Timestamp = DateTime.UtcNow,
            IpAddress = ipAddress,
            RequestSnapshot = requestSnapshot,
            Succeeded = false,
            ErrorMessage = errorMessage
        };
    }
}
