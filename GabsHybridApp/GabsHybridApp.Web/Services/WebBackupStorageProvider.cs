using System.IO;
using GabsHybridApp.Shared.Services;
using Microsoft.AspNetCore.Hosting;

namespace GabsHybridApp.Web.Services;

public class WebBackupStorageProvider : IBackupStorageProvider
{
    private readonly IWebHostEnvironment _env;

    public WebBackupStorageProvider(IWebHostEnvironment env)
    {
        _env = env;
    }

    public string GetBackupStorageDirectory()
    {
        var dir = Path.Combine(_env.ContentRootPath, "Data", "backups");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }
}
