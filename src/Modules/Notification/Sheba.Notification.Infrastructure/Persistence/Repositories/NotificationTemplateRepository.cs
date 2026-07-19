using Microsoft.EntityFrameworkCore;
using Sheba.Notification.Domain.Entities;
using Sheba.Notification.Domain.Interfaces;

namespace Sheba.Notification.Infrastructure.Persistence.Repositories;

public sealed class NotificationTemplateRepository(NotificationDbContext db) : INotificationTemplateRepository
{
    public async Task<NotificationTemplate?> GetByKeyAsync(string templateKey, CancellationToken ct = default)
        => await db.NotificationTemplates.FirstOrDefaultAsync(t => t.TemplateKey == templateKey, ct);

    public async Task AddAsync(NotificationTemplate template, CancellationToken ct = default)
        => await db.NotificationTemplates.AddAsync(template, ct);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
