using System;
using System.Collections.Generic;
using System.Linq;
using GabsHybridApp.Shared.Data;
using GabsHybridApp.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GabsHybridApp.Shared.Services.ModelAdapter;

/// <summary>
/// Service responsible for extracting the live EF Core database schema for backup archives
/// and comparing archive schemas against live models to determine compatibility.
/// </summary>
public class SchemaIntrospectionService
{
    /// <summary>
    /// Extracts comprehensive schema metadata for all entity tables registered in HybridAppDbContext.
    /// This is embedded into manifest.json at backup creation time.
    /// </summary>
    public Dictionary<string, TableSchemaInfo> ExtractDatabaseSchema(HybridAppDbContext db)
    {
        var schemaMap = new Dictionary<string, TableSchemaInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var entityType in db.Model.GetEntityTypes())
        {
            var tableName = entityType.ClrType.Name; // Entity class name used in our backup files
            var tableSchema = new TableSchemaInfo { TableName = tableName };

            foreach (var prop in entityType.GetProperties())
            {
                var clrType = Nullable.GetUnderlyingType(prop.ClrType)?.Name ?? prop.ClrType.Name;
                tableSchema.Columns[prop.Name] = new ColumnSchemaInfo
                {
                    ColumnName = prop.Name,
                    ClrType = clrType,
                    IsNullable = prop.IsNullable,
                    IsPrimaryKey = prop.IsPrimaryKey(),
                    IsForeignKey = prop.IsForeignKey()
                };
            }

            schemaMap[tableName] = tableSchema;
        }

        return schemaMap;
    }

    /// <summary>
    /// Compares archive table schemas with current live database schema.
    /// Determines whether the restore can run via high-performance fast-path native deserialization
    /// or if it requires the adaptive dynamic model adapter.
    /// </summary>
    public SchemaDiffResult CompareSchemas(Dictionary<string, TableSchemaInfo>? archiveSchemas, HybridAppDbContext liveDb)
    {
        var result = new SchemaDiffResult();

        if (archiveSchemas == null || archiveSchemas.Count == 0)
        {
            // Legacy backup without embedded schema metadata
            result.IsPerfectMatch = false;
            result.CanAutoAdapt = true;
            result.Warnings.Add("Legacy archive: Schema metadata not present in manifest. Adaptive deserializer will inspect JSON dynamically.");
            return result;
        }

        var liveSchemas = ExtractDatabaseSchema(liveDb);

        foreach (var liveKvp in liveSchemas)
        {
            var tableName = liveKvp.Key;
            var liveTable = liveKvp.Value;

            if (!archiveSchemas.TryGetValue(tableName, out var archiveTable))
            {
                // Table doesn't exist in archive (could be a newly added entity table in a newer version)
                result.IsPerfectMatch = false;
                result.Warnings.Add($"Table '{tableName}' is in the current database but absent from the backup archive.");
                continue;
            }

            // Check columns
            foreach (var colKvp in liveTable.Columns)
            {
                var colName = colKvp.Key;
                var liveCol = colKvp.Value;

                // Check direct match or alias match
                var matchedArchiveCol = archiveTable.Columns.Values
                    .FirstOrDefault(c => SchemaAliasRegistry.IsMatch(colName, c.ColumnName));

                if (matchedArchiveCol == null)
                {
                    result.IsPerfectMatch = false;
                    if (!result.MissingColumnsPerTable.ContainsKey(tableName))
                        result.MissingColumnsPerTable[tableName] = new List<string>();

                    result.MissingColumnsPerTable[tableName].Add(colName);

                    if (!liveCol.IsNullable && !liveCol.IsPrimaryKey)
                    {
                        result.AutoAdaptedMessages.Add($"Table '{tableName}': Required column '{colName}' missing in backup; will use entity default.");
                    }
                }
                else
                {
                    // Check if matched by alias (not exact name)
                    if (!string.Equals(colName, matchedArchiveCol.ColumnName, StringComparison.Ordinal))
                    {
                        result.IsPerfectMatch = false;
                        result.AutoAdaptedMessages.Add($"Table '{tableName}': Mapped backup property '{matchedArchiveCol.ColumnName}' -> live column '{colName}'.");
                    }

                    // Check type compatibility
                    if (!string.Equals(liveCol.ClrType, matchedArchiveCol.ClrType, StringComparison.OrdinalIgnoreCase))
                    {
                        result.IsPerfectMatch = false;
                        if (!result.TypeMismatchesPerTable.ContainsKey(tableName))
                            result.TypeMismatchesPerTable[tableName] = new List<string>();

                        result.TypeMismatchesPerTable[tableName].Add($"{colName} (Backup: {matchedArchiveCol.ClrType}, Live: {liveCol.ClrType})");
                        result.AutoAdaptedMessages.Add($"Table '{tableName}': Safe type conversion for '{colName}' ({matchedArchiveCol.ClrType} -> {liveCol.ClrType}).");
                    }
                }
            }
        }

        return result;
    }
}
