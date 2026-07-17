using Microsoft.EntityFrameworkCore;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Outbox;

namespace Sheba.Shared.Kernel.Persistence;

/// <summary>Generic <see cref="IInboxGuard"/> over a module's own DbContext (T-EVT-1).</summary>
public sealed class EfInboxGuard<TContext>(TContext context) : IInboxGuard
    where TContext : DbContext
{
    public Task<bool> IsProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
        => context.Set<InboxMessage>()
            .AnyAsync(m => m.EventId == eventId && m.ConsumerName == consumerName, cancellationToken);

    public async Task MarkProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
    {
        context.Set<InboxMessage>().Add(InboxMessage.Create(eventId, consumerName));
        await context.SaveChangesAsync(cancellationToken);
    }
}
