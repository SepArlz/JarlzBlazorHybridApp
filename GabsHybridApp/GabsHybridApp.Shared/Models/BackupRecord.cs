using System;
using System.ComponentModel.DataAnnotations;

namespace GabsHybridApp.Shared.Models;

/// <summary>
/// Audit and catalog record of an exported or staged database backup archive.
/// Preserved permanently across restores to maintain disaster recovery audit trails.
/// </summary>
public class BackupRecord
{
    /// <summary>
    /// Sequential UUIDv7 primary key for chronological sorting and B-Tree index efficiency.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Full archive file name, e.g. "hybridapp_backup_20260918_120000.zip".
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Relative storage path on the server, e.g. "Data/backups/hybridapp_backup_20260918_120000.zip".
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes of the compressed .zip file.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// SHA-256 hexadecimal hash computed at creation time for integrity and tamper verification.
    /// </summary>
    [MaxLength(128)]
    public string? Sha256Checksum { get; set; }

    /// <summary>
    /// Aggregate total number of records across all tables in the archive.
    /// </summary>
    public int TotalRecordsCount { get; set; }

    /// <summary>
    /// Serialized JSON string containing per-table record counts, e.g. {"Products": 24, "Orders": 10}.
    /// </summary>
    public string TableBreakdownJson { get; set; } = "{}";

    /// <summary>
    /// Category/Trigger: "MANUAL" (UI), "ENDPOINT" (/download-db), "PRE_RESTORE_SAFETY" (auto-snapshot).
    /// </summary>
    [MaxLength(50)]
    public string BackupType { get; set; } = "MANUAL";

    /// <summary>
    /// Archive status: "Completed", "Failed", "Restored".
    /// </summary>
    [MaxLength(50)]
    public string Status { get; set; } = "Completed";

    /// <summary>
    /// Web application version that created this backup archive (derived via MinVer).
    /// </summary>
    [MaxLength(50)]
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>
    /// Device form factor that originated this backup: "Web", "Phone", "Tablet", or "Desktop".
    /// </summary>
    [MaxLength(50)]
    public string SourceFormFactor { get; set; } = "Web";

    /// <summary>
    /// OS and platform string where this backup was minted: e.g. "Windows 10.0...", "Android - 14.0".
    /// </summary>
    [MaxLength(100)]
    public string SourcePlatform { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp of backup creation.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Username of the administrator who generated the backup.
    /// </summary>
    [MaxLength(150)]
    public string CreatedBy { get; set; } = "System";

    /// <summary>
    /// Optional administrative notes or memo.
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Soft delete flag.
    /// </summary>
    public bool IsDeleted { get; set; } = false;
}
