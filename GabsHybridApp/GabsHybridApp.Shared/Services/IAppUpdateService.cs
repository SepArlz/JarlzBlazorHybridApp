using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GabsHybridApp.Shared.Services;

public interface IAppUpdateService
{
    /// <summary>
    /// Indicates whether in-app updating is supported on the current platform/runtime (true on Android and Windows MAUI, false on Web).
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Queries the server manifest to check if a newer version is available.
    /// </summary>
    Task<UpdateCheckResult> CheckForUpdatesAsync(bool force = false, CancellationToken ct = default);

    /// <summary>
    /// Streams the update package with progress reporting to a local temporary path.
    /// </summary>
    Task<string> DownloadUpdateAsync(UpdateCheckResult updateInfo, IProgress<double> progress, CancellationToken ct = default);

    /// <summary>
    /// Launches the platform installer / launcher and exits the current app.
    /// </summary>
    Task<bool> InstallUpdateAsync(string localPackagePath);
}

public class UpdateCheckResult
{
    public bool IsUpdateAvailable { get; set; }
    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public long PackageSizeBytes { get; set; }
    public bool IsMandatory { get; set; }
    public string? ErrorMessage { get; set; }

    public string FormattedSize
    {
        get
        {
            if (PackageSizeBytes <= 0) return string.Empty;
            return $"{PackageSizeBytes / (1024.0 * 1024.0):F1} MB";
        }
    }
}

public static class VersionComparisonHelper
{
    /// <summary>
    /// Compares two version strings (e.g. "v1.1.0 (a1b2c3d)", "1.0.0+hash", "1.2.0") and returns true if latest is strictly newer than current.
    /// </summary>
    public static bool IsNewerVersion(string currentVersion, string latestVersion)
    {
        var cleanCurrent = CleanVersionString(currentVersion);
        var cleanLatest = CleanVersionString(latestVersion);

        if (Version.TryParse(cleanCurrent, out var curVer) && Version.TryParse(cleanLatest, out var latVer))
        {
            return latVer > curVer;
        }

        // Fallback: compare major.minor.build components manually
        var curParts = cleanCurrent.Split('.');
        var latParts = cleanLatest.Split('.');
        var maxLen = Math.Max(curParts.Length, latParts.Length);

        for (int i = 0; i < maxLen; i++)
        {
            int cVal = (i < curParts.Length && int.TryParse(curParts[i], out var c)) ? c : 0;
            int lVal = (i < latParts.Length && int.TryParse(latParts[i], out var l)) ? l : 0;

            if (lVal > cVal) return true;
            if (lVal < cVal) return false;
        }

        return false;
    }

    public static string CleanVersionString(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "0.0.0";

        var clean = raw.Trim();
        if (clean.StartsWith('v') || clean.StartsWith('V'))
        {
            clean = clean[1..];
        }

        // Strip commit hash after '+' or '(' or '-'
        int plusIdx = clean.IndexOf('+');
        if (plusIdx >= 0) clean = clean[..plusIdx];

        int parenIdx = clean.IndexOf('(');
        if (parenIdx >= 0) clean = clean[..parenIdx];

        int dashIdx = clean.IndexOf('-');
        if (dashIdx >= 0) clean = clean[..dashIdx];

        clean = clean.Trim();

        // Extract first numeric version pattern e.g. 1.2.3 or 1.2
        var match = Regex.Match(clean, @"^\d+(\.\d+)+");
        if (match.Success)
        {
            clean = match.Value;
        }

        // Ensure at least 2 components for Version.TryParse
        if (!clean.Contains('.'))
        {
            clean = $"{clean}.0";
        }

        return clean;
    }
}
