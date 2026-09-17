using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GabsHybridApp.Shared.Models;

namespace GabsHybridApp.Shared.Services;

/// <summary>
/// Service contract for database-agnostic backup creation, inspection, and wipe-and-restore operations.
/// </summary>
public interface IDataBackupService
{
    /// <summary>
    /// Generates a fresh backup archive zip in the server's backup directory and records it in BackupRecords.
    /// </summary>
    Task<BackupRecord> CreateBackupAsync(string createdBy, string backupType = "MANUAL", string? notes = null, CancellationToken ct = default);

    /// <summary>
    /// Validates an incoming ZIP stream (checks manifest, entry files, counts, and hash) without modifying the database.
    /// </summary>
    Task<BackupValidationResult> ValidateArchiveAsync(Stream zipStream, CancellationToken ct = default);

    /// <summary>
    /// Performs a full database wipe-and-replace using the provided archive stream.
    /// Automatically takes a PRE_RESTORE_SAFETY backup first.
    /// </summary>
    Task<RestoreResult> RestoreFromArchiveAsync(Stream zipStream, string restoredBy, string? originalFileName = null, CancellationToken ct = default);

    /// <summary>
    /// Performs a full database wipe-and-replace using an existing stored backup record from the server.
    /// </summary>
    Task<RestoreResult> RestoreFromStoredBackupAsync(Guid backupId, string restoredBy, CancellationToken ct = default);

    /// <summary>
    /// Gets all backup history records ordered by creation date descending.
    /// </summary>
    Task<List<BackupRecord>> GetBackupHistoryAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the full absolute file path to the latest completed backup archive, or null if none exists.
    /// </summary>
    Task<string?> GetLatestBackupFilePathAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the full absolute file path to a specific backup record.
    /// </summary>
    Task<string?> GetBackupFilePathAsync(Guid backupId, CancellationToken ct = default);

    /// <summary>
    /// Gets the live record counts of all supported domain and lookup tables.
    /// </summary>
    Task<Dictionary<string, int>> GetCurrentTableCountsAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the physical server storage directory where backups are housed.
    /// </summary>
    string GetBackupStorageDirectory();

    /// <summary>
    /// Permanently deletes a specific backup record and removes its physical .zip file from disk.
    /// </summary>
    Task<bool> DeleteBackupAsync(Guid backupId, CancellationToken ct = default);

    /// <summary>
    /// Permanently deletes all backup records and corresponding .zip files created on or before cutoffUtc.
    /// </summary>
    Task<int> DeleteBackupsOlderThanAsync(DateTime cutoffUtc, CancellationToken ct = default);
}
