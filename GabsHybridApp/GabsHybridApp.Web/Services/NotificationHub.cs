using System.Collections.Concurrent;
using System.Security.Claims;
using GabsHybridApp.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace GabsHybridApp.Web.Services;

public class NotificationHub : Hub
{
    // Thread-safe in-memory store for active editor sessions: Key = $"{ConnectionId}_{EntityType}_{EntityId}"
    private static readonly ConcurrentDictionary<string, ActiveEditorDto> _activeEditors = new();

    // In-process server event dispatchers for Blazor Server circuits
    public static event Action<ActiveEditorDto>? OnServerEditorJoined;
    public static event Action<string, string, string?>? OnServerEditorLeft; // entityType, entityKey, connectionId
    public static event Action<DataChangeEventDto>? OnServerDataChanged;

    public static void DispatchServerEditorJoined(ActiveEditorDto editor)
    {
        OnServerEditorJoined?.Invoke(editor);
    }

    public static void DispatchServerEditorLeft(string entityType, string entityKey, string? connectionId = null)
    {
        OnServerEditorLeft?.Invoke(entityType, entityKey, connectionId);
    }

    public static void DispatchServerDataChanged(DataChangeEventDto changeEvent)
    {
        OnServerDataChanged?.Invoke(changeEvent);
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
        }

        // Automatic lock cleanup: purge all active editing sessions held by this disconnected client
        var keysToRemove = _activeEditors
            .Where(kvp => kvp.Value.ConnectionId == Context.ConnectionId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            if (_activeEditors.TryRemove(key, out var editor))
            {
                await Clients.Others.SendAsync("ReceiveEditorLeft", editor.EntityType, editor.EntityKey);
                DispatchServerEditorLeft(editor.EntityType, editor.EntityKey, Context.ConnectionId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Register an active editing session for an entity (e.g. Order #123) and notify other devices.
    /// </summary>
    public async Task StartEditing(string entityType, string entityKey, string userName, string deviceId)
    {
        if (string.IsNullOrWhiteSpace(entityKey) || string.IsNullOrWhiteSpace(entityType)) return;

        var key = $"{Context.ConnectionId}_{entityType}_{entityKey}";
        var editor = new ActiveEditorDto
        {
            ConnectionId = Context.ConnectionId,
            EntityType = entityType,
            EntityKey = entityKey,
            UserName = string.IsNullOrWhiteSpace(userName) ? "Anonymous" : userName,
            DeviceId = string.IsNullOrWhiteSpace(deviceId) ? "Unknown Device" : deviceId,
            StartedAt = DateTime.UtcNow
        };

        _activeEditors[key] = editor;
        await Clients.Others.SendAsync("ReceiveEditorJoined", editor);
        DispatchServerEditorJoined(editor);
    }

    /// <summary>
    /// Release an active editing session and notify other devices.
    /// </summary>
    public async Task StopEditing(string entityType, string entityKey)
    {
        if (string.IsNullOrWhiteSpace(entityKey) || string.IsNullOrWhiteSpace(entityType)) return;

        var key = $"{Context.ConnectionId}_{entityType}_{entityKey}";
        if (_activeEditors.TryRemove(key, out var editor))
        {
            await Clients.Others.SendAsync("ReceiveEditorLeft", entityType, entityKey);
            DispatchServerEditorLeft(entityType, entityKey, Context.ConnectionId);
        }
    }

    /// <summary>
    /// Retrieve currently active editors for a specific entity across all devices.
    /// </summary>
    public Task<List<ActiveEditorDto>> GetActiveEditors(string entityType, string entityKey)
    {
        return Task.FromResult(GetActiveEditorsForEntity(entityType, entityKey, Context.ConnectionId));
    }

    /// <summary>
    /// Broadcast a data change event to all other clients to trigger background silent delta-sync or refresh views.
    /// </summary>
    public async Task BroadcastDataChange(DataChangeEventDto changeEvent)
    {
        await Clients.Others.SendAsync("ReceiveDataChange", changeEvent);
        DispatchServerDataChanged(changeEvent);
    }

    public static void RegisterEditorSession(ActiveEditorDto editor)
    {
        var key = $"{editor.ConnectionId}_{editor.EntityType}_{editor.EntityKey}";
        _activeEditors[key] = editor;
    }

    public static void UnregisterEditorSession(string entityType, string entityKey, string connectionId)
    {
        var key = $"{connectionId}_{entityType}_{entityKey}";
        _activeEditors.TryRemove(key, out _);
    }

    public static List<ActiveEditorDto> GetActiveEditorsForEntity(string entityType, string entityKey, string? excludeConnectionId = null)
    {
        var cutoff = DateTime.UtcNow.AddHours(-1);
        return _activeEditors.Values
            .Where(e => e.EntityType.Equals(entityType, StringComparison.OrdinalIgnoreCase) &&
                        e.EntityKey == entityKey &&
                        e.StartedAt >= cutoff &&
                        (excludeConnectionId == null || e.ConnectionId != excludeConnectionId))
            .ToList();
    }

    private string? GetUserId()
        => Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}