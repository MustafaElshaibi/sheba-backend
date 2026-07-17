namespace Sheba.Shared.Kernel.Interfaces;

/// <summary>
/// Consumer-side idempotency check backed by a module's own inbox_messages table (T-EVT-1).
/// The outbox dispatcher delivers at-least-once, so every notification handler that has a
/// side effect (send email, issue a credential, mutate a read model) should guard itself:
/// <c>if (await guard.IsProcessedAsync(evt.EventId, nameof(MyHandler), ct)) return;</c> before
/// doing the work, then <c>await guard.MarkProcessedAsync(...)</c> after.
/// </summary>
public interface IInboxGuard
{
    Task<bool> IsProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default);
}
