using GabsHybridApp.Shared.Models;

namespace GabsHybridApp.Shared.Services;

public interface INotificationService
{
    event Action<Notification>? OnNotificationReceived;
    Task PushNotificationAsync(Notification notification);
    Task<List<Notification>> GetUserNotificationsAsync(Guid excludeSenderUserId);
}
