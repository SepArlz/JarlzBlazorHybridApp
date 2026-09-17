namespace GabsHybridApp.Shared.Models;

/// <summary>
/// Lightweight DTO broadcast via SignalR when data is created, updated, or deleted.
/// Connected clients use this to trigger debounced silent delta-syncs or refresh active views.
/// </summary>
public class DataChangeEventDto
{
    public string EntityType { get; set; } = string.Empty; // e.g. "Order", "Product", "Lookup"
    public string EntityKey { get; set; } = string.Empty;  // Guid string or int string
    public string Action { get; set; } = "Upsert";         // "Upsert", "Delete"
    public string OriginDeviceId { get; set; } = string.Empty;
    public string OriginUser { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO representing an active editing session on an entity across devices.
/// </summary>
public class ActiveEditorDto
{
    public string ConnectionId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty; // e.g. "Order", "Product"
    public string EntityKey { get; set; } = string.Empty;  // Guid string or int string
    public string UserName { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
}
