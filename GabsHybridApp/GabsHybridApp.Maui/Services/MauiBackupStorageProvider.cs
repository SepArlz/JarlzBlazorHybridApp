using System.IO;
using GabsHybridApp.Shared.Services;
using Microsoft.Maui.Storage;

namespace GabsHybridApp.Maui.Services;

public class MauiBackupStorageProvider : IBackupStorageProvider
{
    public string GetBackupStorageDirectory()
    {
        var dir = Path.Combine(FileSystem.AppDataDirectory, "backups");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }
}
