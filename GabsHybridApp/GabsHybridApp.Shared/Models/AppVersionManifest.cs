namespace GabsHybridApp.Shared.Models;

public class AppVersionManifest
{
    public string Version { get; set; } = "1.0.0";
    public int BuildNumber { get; set; } = 1;
    public string ReleaseNotes { get; set; } = string.Empty;
    public DateTime ReleaseDateUtc { get; set; } = DateTime.UtcNow;
    public string AndroidDownloadUrl { get; set; } = "/download/android";
    public string WindowsDownloadUrl { get; set; } = "/download/windows-exe";
    public string WindowsZipDownloadUrl { get; set; } = "/download/windows";
    public long AndroidSizeBytes { get; set; }
    public long WindowsSizeBytes { get; set; }
    public bool IsMandatory { get; set; } = false;
}
