using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace GabsHybridApp.Shared.Services.ModelAdapter;

/// <summary>
/// Two-tier table deserializer:
/// Tier 1: Fast-path native JsonSerializer deserialization when schemas match 100%.
/// Tier 2: Adaptive dynamic AST deserialization (JsonNode) when schema differences,
/// renames, type coercions, or new required fields are detected.
/// </summary>
public class AdaptiveTableReader
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<T>> ReadTableAsync<T>(
        ZipArchive archive,
        string tableName,
        bool isFastPath,
        CancellationToken ct = default) where T : class, new()
    {
        var entry = archive.GetEntry($"data/{tableName}.json");
        if (entry == null) return new List<T>();

        await using var entryStream = entry.Open();

        // --- TIER 1: FAST-PATH NATIVE TYPED DESERIALIZATION ---
        if (isFastPath)
        {
            var items = await JsonSerializer.DeserializeAsync<List<T>>(entryStream, DefaultJsonOptions, ct);
            return items ?? new List<T>();
        }

        // --- TIER 2: ADAPTIVE DYNAMIC AST MODEL ADAPTER ---
        return await ReadAdaptiveAsync<T>(entryStream, ct);
    }

    private static async Task<List<T>> ReadAdaptiveAsync<T>(Stream stream, CancellationToken ct) where T : class, new()
    {
        JsonNode? rootNode;
        try
        {
            rootNode = await JsonNode.ParseAsync(stream, cancellationToken: ct);
        }
        catch
        {
            return new List<T>();
        }

        if (rootNode is not JsonArray jsonArray)
        {
            return new List<T>();
        }

        var results = new List<T>(jsonArray.Count);
        var targetProps = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var writableProps = new List<PropertyInfo>();
        foreach (var p in targetProps)
        {
            if (p.CanWrite && p.GetSetMethod() != null)
            {
                writableProps.Add(p);
            }
        }

        foreach (var element in jsonArray)
        {
            if (element is not JsonObject jsonObject) continue;

            var entity = new T();
            var sourceKeyNames = new List<string>();
            foreach (var kvp in jsonObject)
            {
                sourceKeyNames.Add(kvp.Key);
            }

            foreach (var prop in writableProps)
            {
                // Find matching property key in the jsonObject (direct, case-insensitive, or alias)
                var matchedKey = SchemaAliasRegistry.FindMatchingSourceProperty(prop.Name, sourceKeyNames);

                if (matchedKey != null && jsonObject.TryGetPropertyValue(matchedKey, out var rawValNode))
                {
                    if (rawValNode == null)
                    {
                        if (Nullable.GetUnderlyingType(prop.PropertyType) != null || !prop.PropertyType.IsValueType)
                        {
                            prop.SetValue(entity, null);
                        }
                    }
                    else
                    {
                        var converted = CoerceValue(rawValNode, prop.PropertyType);
                        if (converted != null)
                        {
                            prop.SetValue(entity, converted);
                        }
                    }
                }
                else
                {
                    // Property is completely missing in backup JSON
                    // If target property is non-nullable string, initialize to string.Empty
                    if (prop.PropertyType == typeof(string))
                    {
                        var currentVal = prop.GetValue(entity);
                        if (currentVal == null)
                        {
                            prop.SetValue(entity, string.Empty);
                        }
                    }
                }
            }

            results.Add(entity);
        }

        return results;
    }

    private static object? CoerceValue(JsonNode node, Type targetType)
    {
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            // If Guid
            if (underlyingType == typeof(Guid))
            {
                var str = node.ToString();
                return Guid.TryParse(str, out var g) ? g : Guid.Empty;
            }

            // If String
            if (underlyingType == typeof(string))
            {
                return node.ToString();
            }

            // If Boolean
            if (underlyingType == typeof(bool))
            {
                if (node is JsonValue val && val.TryGetValue<bool>(out var b))
                    return b;

                var str = node.ToString()?.Trim().ToLowerInvariant();
                return str is "true" or "1" or "yes" or "y";
            }

            // If Int32
            if (underlyingType == typeof(int))
            {
                if (node is JsonValue val && val.TryGetValue<int>(out var i))
                    return i;

                if (int.TryParse(node.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedInt))
                    return parsedInt;

                return 0;
            }

            // If Int64 / Long
            if (underlyingType == typeof(long))
            {
                if (node is JsonValue val && val.TryGetValue<long>(out var l))
                    return l;

                if (long.TryParse(node.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedLong))
                    return parsedLong;

                return 0L;
            }

            // If Decimal
            if (underlyingType == typeof(decimal))
            {
                if (node is JsonValue val && val.TryGetValue<decimal>(out var d))
                    return d;

                if (decimal.TryParse(node.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDec))
                    return parsedDec;

                return 0m;
            }

            // If Double
            if (underlyingType == typeof(double))
            {
                if (node is JsonValue val && val.TryGetValue<double>(out var dbl))
                    return dbl;

                if (double.TryParse(node.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDbl))
                    return parsedDbl;

                return 0.0;
            }

            // If DateTime
            if (underlyingType == typeof(DateTime))
            {
                if (DateTime.TryParse(node.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt))
                    return dt;

                return DateTime.UtcNow;
            }

            // If Enum
            if (underlyingType.IsEnum)
            {
                var str = node.ToString();
                if (Enum.TryParse(underlyingType, str, ignoreCase: true, out var enumVal))
                    return enumVal;

                if (int.TryParse(str, out var enumInt) && Enum.IsDefined(underlyingType, enumInt))
                    return Enum.ToObject(underlyingType, enumInt);

                return Activator.CreateInstance(underlyingType);
            }

            // Fallback: deserialize via System.Text.Json
            return JsonSerializer.Deserialize(node.ToJsonString(), targetType, DefaultJsonOptions);
        }
        catch
        {
            return Nullable.GetUnderlyingType(targetType) != null ? null : (targetType.IsValueType ? Activator.CreateInstance(targetType) : null);
        }
    }
}
