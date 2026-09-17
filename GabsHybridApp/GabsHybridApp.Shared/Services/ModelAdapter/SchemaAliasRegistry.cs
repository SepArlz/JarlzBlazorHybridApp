using System;
using System.Collections.Generic;

namespace GabsHybridApp.Shared.Services.ModelAdapter;

/// <summary>
/// Registry of known historical field and column renames across application releases.
/// Used by the Adaptive Deserializer to automatically bridge schema evolutions during restore.
/// </summary>
public static class SchemaAliasRegistry
{
    // Key: target property name (canonical in current entity model)
    // Value: set of historical or alias names (case-insensitive)
    private static readonly Dictionary<string, HashSet<string>> GlobalAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ContactNumber"] = new(StringComparer.OrdinalIgnoreCase) { "CallerPhone", "Phone", "Contact", "PhoneNumber", "ContactNo", "MobileNumber" },
        ["CreatedAtUtc"] = new(StringComparer.OrdinalIgnoreCase) { "CreatedAt", "CreatedDate", "Created", "CreationTimeUtc", "CreatedUtc", "CreatedOn" },
        ["UpdatedAtUtc"] = new(StringComparer.OrdinalIgnoreCase) { "UpdatedAt", "UpdatedDate", "Updated", "LastModifiedUtc", "ModifiedAtUtc", "UpdatedOn" },
        ["OrderNumber"] = new(StringComparer.OrdinalIgnoreCase) { "OrderNo", "InvoiceNumber", "ReferenceNumber" },
        ["IsDeleted"] = new(StringComparer.OrdinalIgnoreCase) { "Deleted", "Archived", "IsArchived" },
        ["Notes"] = new(StringComparer.OrdinalIgnoreCase) { "Remarks", "Comments", "Description" }
    };

    /// <summary>
    /// Checks if a source column/property name matches the target property name either directly,
    /// case-insensitively, or through known historical aliases.
    /// </summary>
    public static bool IsMatch(string targetPropName, string sourcePropName)
    {
        if (string.Equals(targetPropName, sourcePropName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (GlobalAliases.TryGetValue(targetPropName, out var aliases) && aliases.Contains(sourcePropName))
            return true;

        // Strip underscores or hyphens for normalized comparison
        var normTarget = targetPropName.Replace("_", "").Replace("-", "");
        var normSource = sourcePropName.Replace("_", "").Replace("-", "");
        return string.Equals(normTarget, normSource, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Attempts to find the best source property key in the given dictionary/object that maps to the target property.
    /// </summary>
    public static string? FindMatchingSourceProperty(string targetPropName, IEnumerable<string> availableSourceProps)
    {
        foreach (var src in availableSourceProps)
        {
            if (IsMatch(targetPropName, src))
                return src;
        }
        return null;
    }
}
