using System.IO;

namespace GabsHybridApp.Shared.Services;

/// <summary>
/// Cross-platform abstraction for providing the root directory where database backup archives are housed.
/// </summary>
public interface IBackupStorageProvider
{
    /// <summary>
    /// Gets the absolute physical directory path where backup zip archives should be written and read.
    /// Ensures the directory exists before returning.
    /// </summary>
    string GetBackupStorageDirectory();
}

public class DefaultBackupStorageProvider : IBackupStorageProvider
{
    public string GetBackupStorageDirectory()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Data", "backups");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }
}

