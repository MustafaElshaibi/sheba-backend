using MediatR;

namespace Sheba.Shared.Kernel.Events;

/// <summary>
/// Marker interface for all domain events.
/// Extends MediatR INotification so handlers are discovered automatically.
/// Cross-module communication MUST go through this — never through DbContext injection.
/// </summary>
public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}
