using GabsHybridApp.Shared.Data;
using GabsHybridApp.Shared.Models;
using GabsHybridApp.Shared.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GabsHybridApp.Web.Services;

public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IDbContextFactory<HybridAppDbContext> _dbFactory;

    public static event Action<Notification>? OnServerNotificationReceived;
    public event Action<Notification>? OnNotificationReceived
    {
        add => OnServerNotificationReceived += value;
        remove => OnServerNotificationReceived -= value;
    }

    public NotificationService(IHubContext<NotificationHub> hubContext, IDbContextFactory<HybridAppDbContext> dbFactory)
    {
        _hubContext = hubContext;
        _dbFactory = dbFactory;
    }

    public async Task PushNotificationAsync(Notification notification)
    {
        notification.CreatedOn = DateTime.Now;
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        // Notify in-process Blazor Server circuits immediately
        OnServerNotificationReceived?.Invoke(notification);

        // Push to all remote SignalR clients (mobile / MAUI)
        await _hubContext.Clients.All.SendAsync("ReceiveNotification", notification);
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
