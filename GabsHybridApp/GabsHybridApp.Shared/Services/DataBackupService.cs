using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using GabsHybridApp.Shared.Data;
using GabsHybridApp.Shared.Models;
using GabsHybridApp.Shared.Services.ModelAdapter;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GabsHybridApp.Shared.Services;

/// <summary>
/// Cross-platform, database-agnostic backup and restore service (Web & MAUI).
/// Streams entity tables to JSON files within a ZIP archive, calculates SHA256 integrity,
/// captures platform and form factor metadata, embeds full schema definitions for model adaptation,
/// and executes topological wipe-and-replace using a two-tier (fast-path + adaptive) restore engine.
/// </summary>
public class DataBackupService : IDataBackupService
{
    private readonly IDbContextFactory<HybridAppDbContext> _dbFactory;
    private readonly IBackupStorageProvider _storageProvider;
    private readonly IFormFactor _formFactor;
    private readonly SchemaIntrospectionService _schemaIntrospection;
    private readonly AdaptiveTableReader _adaptiveReader;
    private readonly ILogger<DataBackupService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    public DataBackupService(
        IDbContextFactory<HybridAppDbContext> dbFactory,
        IBackupStorageProvider storageProvider,
        IFormFactor formFactor,
        SchemaIntrospectionService schemaIntrospection,
        AdaptiveTableReader adaptiveReader,
        ILogger<DataBackupService> logger)
    {
        _dbFactory = dbFactory;
        _storageProvider = storageProvider;
        _formFactor = formFactor;
        _schemaIntrospection = schemaIntrospection;
        _adaptiveReader = adaptiveReader;
        _logger = logger;
    }

    public string GetBackupStorageDirectory()
    {
        return _storageProvider.GetBackupStorageDirectory();
    }

    public async Task<Dictionary<string, int>> GetCurrentTableCountsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Products"] = await db.Products.CountAsync(ct),
            ["Orders"] = await db.Orders.Where(o => !o.IsDeleted).CountAsync(ct),
            ["OrderItems"] = await db.OrderItems.CountAsync(ct),
            ["UserAccounts"] = await db.UserAccounts.CountAsync(ct),
            ["RegisteredDevices"] = await db.RegisteredDevices.CountAsync(ct),
            ["Notifications"] = await db.Notifications.CountAsync(ct)
        };
        return counts;
    }

    public async Task<BackupRecord> CreateBackupAsync(string createdBy, string backupType = "MANUAL", string? notes = null, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var fileName = $"hybridapp_backup_{now:yyyyMMdd_HHmmss}.zip";
        var storageDir = GetBackupStorageDirectory();
        var fullPath = Path.Combine(storageDir, fileName);
        var relativePath = Path.Combine("Data", "backups", fileName).Replace('\\', '/');

        var formFactor = _formFactor.GetFormFactor();
        var platform = _formFactor.GetPlatform();

        _logger.LogInformation("Starting database backup archive creation: {FileName} by {User} on [{FormFactor} / {Platform}]",
            fileName, createdBy, formFactor, platform);

        var manifest = new BackupManifest
        {
            System = "GabsBlazorHybridApp",
            Version = AppVersionInfo.DisplayVersionNoSha,
            GitHash = AppVersionInfo.ShortGitHash,
            SchemaVersion = 1,
            SourceFormFactor = formFactor,
            SourcePlatform = platform,
            CreatedAtUtc = now,
            ExportedBy = createdBy
        };

        var tableCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        await using (var db = await _dbFactory.CreateDbContextAsync(ct))
        {
            manifest.TableSchemas = _schemaIntrospection.ExtractDatabaseSchema(db);

            await using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                // 1. Products
                tableCounts["Products"] = await WriteTableJsonAsync(zipArchive, "Products", await db.Products.AsNoTracking().ToListAsync(ct));

                // 2. Orders & Items
                var activeOrders = await db.Orders.AsNoTracking().Where(o => !o.IsDeleted).ToListAsync(ct);
                tableCounts["Orders"] = await WriteTableJsonAsync(zipArchive, "Orders", activeOrders);

                var activeOrderIds = activeOrders.Select(o => o.Id).ToHashSet();
                var activeItems = await db.OrderItems.AsNoTracking().Where(i => activeOrderIds.Contains(i.OrderId)).ToListAsync(ct);
                tableCounts["OrderItems"] = await WriteTableJsonAsync(zipArchive, "OrderItems", activeItems);

                // 3. User Accounts
                tableCounts["UserAccounts"] = await WriteTableJsonAsync(zipArchive, "UserAccounts", await db.UserAccounts.AsNoTracking().ToListAsync(ct));

                // 4. Registered Devices
                tableCounts["RegisteredDevices"] = await WriteTableJsonAsync(zipArchive, "RegisteredDevices", await db.RegisteredDevices.AsNoTracking().ToListAsync(ct));

                // 5. Notifications
                tableCounts["Notifications"] = await WriteTableJsonAsync(zipArchive, "Notifications", await db.Notifications.AsNoTracking().ToListAsync(ct));

                // 6. Write manifest.json
                manifest.Tables = tableCounts;
                var manifestEntry = zipArchive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                await using (var manifestStream = manifestEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, ct);
                }
            }
        }

        // Compute File Size and SHA-256 Checksum
        var fileInfo = new FileInfo(fullPath);
        var sizeBytes = fileInfo.Length;
        string sha256Hex;
        using (var sha = SHA256.Create())
        await using (var readStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var hashBytes = await sha.ComputeHashAsync(readStream, ct);
            sha256Hex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        var totalRecords = tableCounts.Values.Sum();

        var record = new BackupRecord
        {
            Id = Guid.CreateVersion7(),
            FileName = fileName,
            FilePath = relativePath,
            FileSizeBytes = sizeBytes,
            Sha256Checksum = sha256Hex,
            TotalRecordsCount = totalRecords,
            TableBreakdownJson = JsonSerializer.Serialize(tableCounts),
            BackupType = backupType,
            Status = "Completed",
            AppVersion = AppVersionInfo.DisplayVersionNoSha,
            SourceFormFactor = formFactor,
            SourcePlatform = platform,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            Notes = notes,
            IsDeleted = false
        };

        await using (var db = await _dbFactory.CreateDbContextAsync(ct))
        {
            db.BackupRecords.Add(record);
            await db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Backup archive created successfully: {FileName} ({Bytes} bytes, {Records} records, Platform: [{FormFactor} / {Platform}], SHA: {Sha})",
            fileName, sizeBytes, totalRecords, formFactor, platform, sha256Hex[..Math.Min(8, sha256Hex.Length)]);

        return record;
    }

    public async Task<BackupValidationResult> ValidateArchiveAsync(Stream zipStream, CancellationToken ct = default)
    {
        var result = new BackupValidationResult();
        try
        {
            if (zipStream.CanSeek)
            {
                result.FileSizeBytes = zipStream.Length;
                zipStream.Position = 0;
            }

            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
            var manifestEntry = archive.GetEntry("manifest.json");
            if (manifestEntry == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "Invalid backup archive: 'manifest.json' was not found in the root of the ZIP file.";
                return result;
            }

            await using (var stream = manifestEntry.Open())
            {
                result.Manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(stream, JsonOptions, ct);
            }

            if (result.Manifest == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "Failed to parse manifest.json: Invalid or corrupt JSON.";
                return result;
            }

            result.TableCounts = result.Manifest.Tables ?? new(StringComparer.OrdinalIgnoreCase);

            if (zipStream.CanSeek)
            {
                zipStream.Position = 0;
                using var sha = SHA256.Create();
                var hashBytes = await sha.ComputeHashAsync(zipStream, ct);
                result.ComputedSha256 = Convert.ToHexString(hashBytes).ToLowerInvariant();
                zipStream.Position = 0;
            }

            await using (var db = await _dbFactory.CreateDbContextAsync(ct))
            {
                result.SchemaDiff = _schemaIntrospection.CompareSchemas(result.Manifest.TableSchemas, db);
            }

            result.IsValid = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup archive validation encountered an unhandled exception.");
            result.IsValid = false;
            result.ErrorMessage = $"Failed to validate archive: {ex.Message}";
            return result;
        }
    }

    public async Task<RestoreResult> RestoreFromStoredBackupAsync(Guid backupId, string restoredBy, CancellationToken ct = default)
    {
        var fullPath = await GetBackupFilePathAsync(backupId, ct);
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
        {
            return new RestoreResult { Success = false, ErrorMessage = "Stored backup archive file was not found on disk." };
        }

        await using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await RestoreFromArchiveAsync(fileStream, restoredBy, Path.GetFileName(fullPath), ct);
    }

    public async Task<RestoreResult> RestoreFromArchiveAsync(Stream zipStream, string restoredBy, string? originalFileName = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Beginning database wipe-and-restore initiated by {User} on [{FormFactor}]", restoredBy, _formFactor.GetFormFactor());

        var validation = await ValidateArchiveAsync(zipStream, ct);
        if (!validation.IsValid)
        {
            return new RestoreResult
            {
                Success = false,
                ErrorMessage = validation.ErrorMessage ?? "Archive validation failed."
            };
        }

        var isFastPath = validation.SchemaDiff?.IsPerfectMatch ?? false;
        _logger.LogInformation("Restore engine strategy selected: {Strategy}", isFastPath ? "TIER 1 (FAST-PATH NATIVE)" : "TIER 2 (ADAPTIVE MODEL ADAPTER)");

        Guid? safetyId = null;
        try
        {
            var safety = await CreateBackupAsync(restoredBy, "PRE_RESTORE_SAFETY", "Automated pre-restore snapshot before database wipe", ct);
            safetyId = safety.Id;
            _logger.LogInformation("Captured pre-restore safety snapshot: {SafetyFileName}", safety.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture pre-restore safety snapshot. Aborting restore to protect live data.");
            return new RestoreResult
            {
                Success = false,
                ErrorMessage = $"Safety snapshot failed: {ex.Message}. Database was not modified."
            };
        }

        if (zipStream.CanSeek)
        {
            zipStream.Position = 0;
        }

        var restoredCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            if (zipStream.CanSeek)
            {
                zipStream.Position = 0;
            }
            restoredCounts.Clear();
            db.ChangeTracker.Clear();

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            try
            {
                // --- A. TOPOLOGICAL WIPE (Child / Dependent first) ---
                db.OrderItems.RemoveRange(await db.OrderItems.ToListAsync(ct));
                db.Orders.RemoveRange(await db.Orders.IgnoreQueryFilters().ToListAsync(ct));
                db.Products.RemoveRange(await db.Products.ToListAsync(ct));
                db.Notifications.RemoveRange(await db.Notifications.ToListAsync(ct));
                db.RegisteredDevices.RemoveRange(await db.RegisteredDevices.ToListAsync(ct));

                // Retain existing users if no users present in archive, otherwise update/replace non-admin
                var usersInArchiveCount = validation.TableCounts.GetValueOrDefault("UserAccounts", 0);
                if (usersInArchiveCount > 0)
                {
                    db.UserAccounts.RemoveRange(await db.UserAccounts.ToListAsync(ct));
                }

                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Topological database wipe complete. Inserting archived tables via {Strategy}...",
                    isFastPath ? "Fast-Path Native Deserialization" : "Adaptive Dynamic Model Adapter");

                // --- B. TOPOLOGICAL RESTORE (Parent / Independent first) ---
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);

                // 1. Products
                var products = await _adaptiveReader.ReadTableAsync<Product>(archive, "Products", isFastPath, ct);
                if (products.Count > 0)
                {
                    db.Products.AddRange(products);
                    restoredCounts["Products"] = products.Count;
                }

                // 2. Orders
                var orders = await _adaptiveReader.ReadTableAsync<Order>(archive, "Orders", isFastPath, ct);
                if (orders.Count > 0)
                {
                    db.Orders.AddRange(orders);
                    restoredCounts["Orders"] = orders.Count;
                }

                // 3. OrderItems
                var orderItems = await _adaptiveReader.ReadTableAsync<OrderItem>(archive, "OrderItems", isFastPath, ct);
                if (orderItems.Count > 0)
                {
                    db.OrderItems.AddRange(orderItems);
                    restoredCounts["OrderItems"] = orderItems.Count;
                }

                // 4. Users
                if (usersInArchiveCount > 0)
                {
                    var users = await _adaptiveReader.ReadTableAsync<UserAccount>(archive, "UserAccounts", isFastPath, ct);
                    if (users.Count > 0)
                    {
                        db.UserAccounts.AddRange(users);
                        restoredCounts["UserAccounts"] = users.Count;
                    }
                }

                // 5. Registered Devices
                var devices = await _adaptiveReader.ReadTableAsync<RegisteredDevice>(archive, "RegisteredDevices", isFastPath, ct);
                if (devices.Count > 0)
                {
                    db.RegisteredDevices.AddRange(devices);
                    restoredCounts["RegisteredDevices"] = devices.Count;
                }

                // 6. Notifications
                var notifications = await _adaptiveReader.ReadTableAsync<Notification>(archive, "Notifications", isFastPath, ct);
                if (notifications.Count > 0)
                {
                    db.Notifications.AddRange(notifications);
                    restoredCounts["Notifications"] = notifications.Count;
                }

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                sw.Stop();

                var totalRestored = restoredCounts.Values.Sum();
                _logger.LogInformation("Database restore completed successfully: {Count} total records restored across {Tables} tables in {Elapsed}ms (Adaptive: {UsedAdaptive})",
                    totalRestored, restoredCounts.Count, sw.ElapsedMilliseconds, !isFastPath);

                // Save restored archive copy to local backup storage and catalog in BackupRecords
                try
                {
                    var archiveName = !string.IsNullOrWhiteSpace(originalFileName)
                        ? Path.GetFileName(originalFileName)
                        : $"hybridapp_backup_{validation.Manifest?.CreatedAtUtc:yyyyMMdd_HHmmss}.zip";

                    var storageDir = GetBackupStorageDirectory();
                    var targetPath = Path.Combine(storageDir, archiveName);

                    if (!File.Exists(targetPath) && zipStream.CanSeek)
                    {
                        zipStream.Position = 0;
                        await using var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
                        await zipStream.CopyToAsync(fs, ct);
                    }

                    var relativePath = Path.Combine("Data", "backups", archiveName).Replace('\\', '/');
                    var fileSize = File.Exists(targetPath) ? new FileInfo(targetPath).Length : (zipStream.CanSeek ? zipStream.Length : 0);

                    var existing = await db.BackupRecords.FirstOrDefaultAsync(b => b.FileName == archiveName, ct);
                    if (existing == null)
                    {
                        var restoredRecord = new BackupRecord
                        {
                            Id = Guid.CreateVersion7(),
                            FileName = archiveName,
                            FilePath = relativePath,
                            FileSizeBytes = fileSize,
                            Sha256Checksum = validation.ComputedSha256 ?? string.Empty,
                            TotalRecordsCount = totalRestored,
                            TableBreakdownJson = JsonSerializer.Serialize(restoredCounts),
                            BackupType = "RESTORED",
                            Status = "Completed",
                            AppVersion = validation.Manifest?.Version ?? AppVersionInfo.DisplayVersionNoSha,
                            SourceFormFactor = validation.Manifest?.SourceFormFactor ?? _formFactor.GetFormFactor(),
                            SourcePlatform = validation.Manifest?.SourcePlatform ?? _formFactor.GetPlatform(),
                            CreatedAtUtc = validation.Manifest?.CreatedAtUtc ?? DateTime.UtcNow,
                            CreatedBy = validation.Manifest?.ExportedBy ?? restoredBy,
                            Notes = $"Imported and restored on {_formFactor.GetFormFactor()} by {restoredBy}",
                            IsDeleted = false
                        };
                        db.BackupRecords.Add(restoredRecord);
                        await db.SaveChangesAsync(ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to catalog restored archive in BackupRecords");
                }

                return new RestoreResult
                {
                    Success = true,
                    TotalRecordsRestored = totalRestored,
                    RestoredTableCounts = restoredCounts,
                    Duration = sw.Elapsed,
                    SafetyBackupId = safetyId,
                    UsedAdaptiveDeserializer = !isFastPath
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                sw.Stop();
                _logger.LogError(ex, "Database restore failed. Rolled back transaction. Database state preserved.");
                var detail = ex.InnerException != null ? $": {ex.InnerException.Message}" : string.Empty;
                return new RestoreResult
                {
                    Success = false,
                    ErrorMessage = $"Restore failed and rolled back: {ex.Message}{detail}",
                    SafetyBackupId = safetyId,
                    Duration = sw.Elapsed
                };
            }
        });
    }

    public async Task<List<BackupRecord>> GetBackupHistoryAsync(CancellationToken ct = default)
    {
        await SyncBackupsFromDiskAsync(ct);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.BackupRecords
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAtUtc)
            .ToListAsync(ct);
    }

    private async Task SyncBackupsFromDiskAsync(CancellationToken ct)
    {
        try
        {
            var storageDir = GetBackupStorageDirectory();
            if (!Directory.Exists(storageDir)) return;

            var zipFiles = Directory.GetFiles(storageDir, "*.zip");
            if (zipFiles.Length == 0) return;

            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var existingFileNames = await db.BackupRecords.Select(b => b.FileName).ToHashSetAsync(ct);

            bool hasNew = false;
            foreach (var file in zipFiles)
            {
                var fileName = Path.GetFileName(file);
                if (existingFileNames.Contains(fileName)) continue;

                try
                {
                    using var archive = ZipFile.OpenRead(file);
                    var manifestEntry = archive.GetEntry("manifest.json");
                    if (manifestEntry != null)
                    {
                        await using var stream = manifestEntry.Open();
                        var manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(stream, JsonOptions, ct);
                        if (manifest != null)
                        {
                            var fileInfo = new FileInfo(file);
                            var totalRecords = manifest.Tables?.Values.Sum() ?? 0;
                            var record = new BackupRecord
                            {
                                Id = Guid.CreateVersion7(),
                                FileName = fileName,
                                FilePath = Path.Combine("Data", "backups", fileName).Replace('\\', '/'),
                                FileSizeBytes = fileInfo.Length,
                                Sha256Checksum = string.Empty,
                                TotalRecordsCount = totalRecords,
                                TableBreakdownJson = JsonSerializer.Serialize(manifest.Tables ?? new()),
                                BackupType = fileName.Contains("SAFETY", StringComparison.OrdinalIgnoreCase) ? "PRE_RESTORE_SAFETY" : "MANUAL",
                                Status = "Completed",
                                AppVersion = manifest.Version ?? AppVersionInfo.DisplayVersionNoSha,
                                SourceFormFactor = manifest.SourceFormFactor ?? "Web",
                                SourcePlatform = manifest.SourcePlatform ?? string.Empty,
                                CreatedAtUtc = manifest.CreatedAtUtc != default ? manifest.CreatedAtUtc : fileInfo.CreationTimeUtc,
                                CreatedBy = manifest.ExportedBy ?? "System",
                                Notes = "Discovered from local backup storage directory",
                                IsDeleted = false
                            };
                            db.BackupRecords.Add(record);
                            hasNew = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse manifest for uncatalogued backup file {File}", file);
                }
            }

            if (hasNew)
            {
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error syncing backup archives from disk");
        }
    }

    public async Task<string?> GetLatestBackupFilePathAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var latest = await db.BackupRecords
            .AsNoTracking()
            .Where(b => b.Status == "Completed")
            .OrderByDescending(b => b.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        var storageDir = GetBackupStorageDirectory();

        if (latest != null)
        {
            var fullPath = Path.Combine(storageDir, latest.FileName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        var files = Directory.GetFiles(storageDir, "*.zip")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTimeUtc)
            .ToList();

        return files.FirstOrDefault()?.FullName;
    }

    public async Task<string?> GetBackupFilePathAsync(Guid backupId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var record = await db.BackupRecords.AsNoTracking().FirstOrDefaultAsync(b => b.Id == backupId, ct);
        if (record == null) return null;

        var storageDir = GetBackupStorageDirectory();
        var fullPath = Path.Combine(storageDir, record.FileName);
        return File.Exists(fullPath) ? fullPath : null;
    }

    public async Task<bool> DeleteBackupAsync(Guid backupId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var record = await db.BackupRecords.FirstOrDefaultAsync(b => b.Id == backupId, ct);
        if (record == null) return false;

        var storageDir = GetBackupStorageDirectory();
        var fullPath = Path.Combine(storageDir, record.FileName);
        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete physical backup file {Path}", fullPath);
        }

        db.BackupRecords.Remove(record);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Deleted backup record {Id} ({FileName})", backupId, record.FileName);
        return true;
    }

    public async Task<int> DeleteBackupsOlderThanAsync(DateTime cutoffUtc, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var records = await db.BackupRecords
            .Where(b => b.CreatedAtUtc <= cutoffUtc)
            .ToListAsync(ct);

        if (records.Count == 0) return 0;

        var storageDir = GetBackupStorageDirectory();
        foreach (var rec in records)
        {
            var fullPath = Path.Combine(storageDir, rec.FileName);
            try
            {
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete physical backup file {Path}", fullPath);
            }
        }

        db.BackupRecords.RemoveRange(records);
        var count = await db.SaveChangesAsync(ct);
        _logger.LogInformation("Purged {Count} backup records created on or before {Cutoff}", records.Count, cutoffUtc);
        return records.Count;
    }

    private static async Task<int> WriteTableJsonAsync<T>(ZipArchive archive, string tableName, List<T> items)
    {
        var entry = archive.CreateEntry($"data/{tableName}.json", CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        await JsonSerializer.SerializeAsync(entryStream, items, JsonOptions);
        return items.Count;
    }
}
