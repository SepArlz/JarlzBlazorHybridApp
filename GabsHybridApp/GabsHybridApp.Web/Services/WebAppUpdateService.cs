using System;
using System.Threading;
using System.Threading.Tasks;
using GabsHybridApp.Shared.Services;

namespace GabsHybridApp.Web.Services;

public class WebAppUpdateService : IAppUpdateService
{
    public bool IsSupported => false;

    public Task<UpdateCheckResult> CheckForUpdatesAsync(bool force = false, CancellationToken ct = default)
    {
        return Task.FromResult(new UpdateCheckResult
        {
            IsUpdateAvailable = false,
            CurrentVersion = AppVersionInfo.Version,
            LatestVersion = AppVersionInfo.Version,
            ReleaseNotes = "Web application updates automatically on the server."
        });
    }

    public Task<string> DownloadUpdateAsync(UpdateCheckResult updateInfo, IProgress<double> progress, CancellationToken ct = default)
    {
        throw new NotSupportedException("In-app downloading is not supported on Web. The browser automatically receives server updates.");
    }

    public Task<bool> InstallUpdateAsync(string localPackagePath)
    {
        return Task.FromResult(false);
    }
}
