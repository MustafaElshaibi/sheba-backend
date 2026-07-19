using Sheba.Notification.Domain.Interfaces;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Notification.Infrastructure.Services;

public sealed class NotificationTemplateService(INotificationTemplateRepository repository)
    : INotificationTemplateService
{
    public async Task<RenderedNotification> RenderAsync(
        string templateKey, IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default)
    {
        var template = await repository.GetByKeyAsync(templateKey, ct)
            ?? throw new InvalidOperationException($"Notification template '{templateKey}' is not seeded.");

        return template.Render(tokens);
    }
}
