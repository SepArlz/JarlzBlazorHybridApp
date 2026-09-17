using System;
using System.Collections.Generic;

namespace GabsHybridApp.Shared.Models;

/// <summary>
/// Manifest embedded at the root of every backup zip archive ('manifest.json').
/// Guarantees archive integrity, schema versioning, and per-table record tallies.
/// </summary>
public class BackupManifest
{
    public string System { get; set; } = "GabsBlazorHybridApp";
    public string Version { get; set; } = Services.AppVersionInfo.DisplayVersion;
    public string? GitHash { get; set; } = Services.AppVersionInfo.ShortGitHash;
    public int SchemaVersion { get; set; } = 1;
    public string SourceFormFactor { get; set; } = "Web";
    public string SourcePlatform { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string ExportedBy { get; set; } = "System";
    public Dictionary<string, int> Tables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, TableSchemaInfo> TableSchemas { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Detailed schema definition of an individual entity table stored in the backup archive.
/// </summary>
public class TableSchemaInfo
{
    public string TableName { get; set; } = string.Empty;
    public Dictionary<string, ColumnSchemaInfo> Columns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Detailed property/column definition for schema comparison and adaptation during restore.
/// </summary>
public class ColumnSchemaInfo
{
    public string ColumnName { get; set; } = string.Empty;
    public string ClrType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
}

/// <summary>
/// Result of comparing archive TableSchemas against the live EF Core database model.
/// </summary>
public class SchemaDiffResult
{
    public bool IsPerfectMatch { get; set; } = true;
    public bool CanAutoAdapt { get; set; } = true;
    public List<string> Warnings { get; set; } = new();
    public List<string> AutoAdaptedMessages { get; set; } = new();
    public Dictionary<string, List<string>> MissingColumnsPerTable { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> ExtraColumnsPerTable { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> TypeMismatchesPerTable { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Pre-restore validation inspection result.
/// </summary>
public class BackupValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public BackupManifest? Manifest { get; set; }
    public Dictionary<string, int> TableCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public long FileSizeBytes { get; set; }
    public string? ComputedSha256 { get; set; }
    public SchemaDiffResult? SchemaDiff { get; set; }
}

/// <summary>
/// Post-restore execution summary.
/// </summary>
public class RestoreResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalRecordsRestored { get; set; }
    public Dictionary<string, int> RestoredTableCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public TimeSpan Duration { get; set; }
    public Guid? SafetyBackupId { get; set; }
    public bool UsedAdaptiveDeserializer { get; set; }
}
