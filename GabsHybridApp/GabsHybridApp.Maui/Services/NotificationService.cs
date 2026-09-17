using GabsHybridApp.Shared.Data;
using GabsHybridApp.Shared.Models;
using GabsHybridApp.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace GabsHybridApp.Maui.Services;

public class NotificationService : INotificationService
{
    private readonly IDbContextFactory<HybridAppDbContext> _dbFactory;

    public event Action<Notification>? OnNotificationReceived { add { } remove { } }

    public NotificationService(IDbContextFactory<HybridAppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task PushNotificationAsync(Notification notification)
    {
        notification.CreatedOn = DateTime.Now;
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
    }

    public async Task<List<Notification>> GetUserNotificationsAsync(Guid excludeSenderUserId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Notifications
            .Where(n => n.UserId != excludeSenderUserId)
            .OrderByDescending(n => n.CreatedOn)
            .AsNoTracking()
            .ToListAsync();
    }
}
