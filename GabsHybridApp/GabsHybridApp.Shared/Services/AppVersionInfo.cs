using System.Reflection;
using Microsoft.JSInterop;

namespace GabsHybridApp.Shared.Services;

/// <summary>
/// Provides release versioning, Git commit metadata, and attribution info derived automatically at compile time.
/// </summary>
public static class AppVersionInfo
{
    private static string? _cachedVersion;
    private static string? _cachedGitHash;
    private static string? _cachedDisplayVersion;
    private static string? _cachedFullInformationalVersion;

    public static string Version
    {
        get
        {
            EnsureVersionCached();
            return _cachedVersion!;
        }
    }

    public static string? ShortGitHash
    {
        get
        {
            EnsureVersionCached();
            return _cachedGitHash;
        }
    }

    public static string DisplayVersion
    {
        get
        {
            EnsureVersionCached();
            return _cachedDisplayVersion!;
        }
    }

    public static string DisplayVersionNoSha
    {
        get
        {
            EnsureVersionCached();
            var prefix = _cachedVersion!.StartsWith('v') ? "" : "v";
            return $"{prefix}{_cachedVersion}";
        }
    }

    public static string FullInformationalVersion
    {
        get
        {
            EnsureVersionCached();
            return _cachedFullInformationalVersion!;
        }
    }

    private static void EnsureVersionCached()
    {
        if (_cachedVersion != null) return;

        var assembly = typeof(AppVersionInfo).Assembly;
        var infoVerAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        _cachedFullInformationalVersion = infoVerAttr ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(infoVerAttr))
        {
            var parts = infoVerAttr.Split('+');
            _cachedVersion = parts[0];

            // Normalize untagged MinVer preview versions (e.g. "1.0.0-preview.0.154" or "0.0.0-preview.0.154") to "1.0.154"
            if (_cachedVersion.Contains("-preview.0.") || _cachedVersion.Contains("-alpha.0."))
            {
                var dotIdx = _cachedVersion.LastIndexOf('.');
                if (dotIdx >= 0 && dotIdx < _cachedVersion.Length - 1)
                {
                    var commitHeight = _cachedVersion.Substring(dotIdx + 1);
                    _cachedVersion = $"1.0.{commitHeight}";
                }
            }

            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                var fullSha = parts[1].Trim();
                _cachedGitHash = fullSha.Length > 7 ? fullSha[..7] : fullSha;
            }
        }
        else
        {
            var asmVer = assembly.GetName().Version;
            _cachedVersion = asmVer != null ? $"{asmVer.Major}.{asmVer.Minor}.{asmVer.Build}" : "1.0.0";
            _cachedGitHash = null;
        }

        var prefix = _cachedVersion.StartsWith('v') ? "" : "v";
        if (!string.IsNullOrEmpty(_cachedGitHash))
        {
            _cachedDisplayVersion = $"{prefix}{_cachedVersion} ({_cachedGitHash})";
        }
        else
        {
            _cachedDisplayVersion = $"{prefix}{_cachedVersion}";
        }
    }

    /// <summary>
    /// Generates a diagnostic string suitable for troubleshooting and issue reports.
    /// </summary>
    public static string GetDiagnosticSummary(IFormFactor? formFactor = null)
    {
        var platform = formFactor != null ? $"{formFactor.GetFormFactor()} ({formFactor.GetPlatform()})" : "Unknown Platform";
        var runtime = $".NET {Environment.Version}";
        var os = Environment.OSVersion.ToString();

        return $"""
        Application: Jarlz MotoHub
        Version: {DisplayVersion}
        Full Version: {FullInformationalVersion}
        Platform: {platform}
        Runtime: {runtime}
        OS: {os}
        """;
    }

    public static void PrewarmProductionPipeline(bool isProduction)
    {
        CircuitBackpressureCoordinator.RestoreCadenceOptimal();
    }
}
