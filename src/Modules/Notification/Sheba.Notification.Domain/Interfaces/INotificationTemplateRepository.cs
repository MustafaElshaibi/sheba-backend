using Sheba.Notification.Domain.Entities;

namespace Sheba.Notification.Domain.Interfaces;

public interface INotificationTemplateRepository
{
    Task<NotificationTemplate?> GetByKeyAsync(string templateKey, CancellationToken ct = default);
    Task AddAsync(NotificationTemplate template, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
