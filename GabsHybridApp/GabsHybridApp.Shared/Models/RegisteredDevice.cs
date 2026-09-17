using System.ComponentModel.DataAnnotations;

namespace GabsHybridApp.Shared.Models;

public class RegisteredDevice
{
    [Key]
    [StringLength(50)]
    public string DeviceId { get; set; } = string.Empty; // e.g. "POS-TERMINAL-01", "MOBILE-TAB-02"

    [Required]
    [StringLength(100)]
    public string DeviceName { get; set; } = string.Empty; // "Counter POS Terminal #1"

    [StringLength(150)]
    public string? AssignedLocation { get; set; } // "Main Store Counter / Warehouse"

    public bool IsActive { get; set; } = true; // Admin kill-switch

    public DateTime RegisteredOn { get; set; } = DateTime.UtcNow;

    public DateTime? LastSyncUtc { get; set; }
}
