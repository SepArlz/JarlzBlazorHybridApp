namespace GabsHybridApp.Shared.Models;

public class DeviceBatchUserSyncRequest
{
    public List<string> Usernames { get; set; } = new();
    public string? DeviceId { get; set; }
}

public class DeviceHeartbeatRequest
{
    public string DeviceId { get; set; } = string.Empty;
}

public sealed record MobileLoginRequest(string Username, string Password, string? DeviceId = null);

public sealed class MobileLoginResponse
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public byte[]? PasswordHash { get; set; }
    public byte[]? PasswordSalt { get; set; }
    public string? Roles { get; set; }
    public bool IsActive { get; set; }
    public string? ServerSalt { get; set; }
    public DateTime CreatedOn { get; set; }
}
