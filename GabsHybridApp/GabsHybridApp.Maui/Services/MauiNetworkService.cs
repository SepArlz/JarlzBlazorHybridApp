using GabsHybridApp.Shared.Services;

namespace GabsHybridApp.Maui.Services;

public sealed class MauiNetworkService : INetworkService, IDisposable
{
    public bool IsOnline { get; private set; }
    public bool IsWifi { get; private set; }
    public bool IsCellular { get; private set; }

    public MauiNetworkService()
    {
        // seed
        Apply(Connectivity.Current.NetworkAccess, Connectivity.Current.ConnectionProfiles);
        // watch for changes
        Connectivity.Current.ConnectivityChanged += OnChanged;
    }

    // Rename of the earlier VerifyInternetReachableAsync -> Refresh()
    public Task RefreshAsync(CancellationToken ct = default)
    {
        Apply(Connectivity.Current.NetworkAccess, Connectivity.Current.ConnectionProfiles);
        return Task.CompletedTask;
    }

    public Task OpenSettingsAsync() => MainThread.InvokeOnMainThreadAsync(AppInfo.ShowSettingsUI);

    private void OnChanged(object? s, ConnectivityChangedEventArgs e)
        => Apply(e.NetworkAccess, e.ConnectionProfiles);

    private void Apply(NetworkAccess access, IEnumerable<ConnectionProfile> profiles)
    {
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            IsOnline = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable() || access is not NetworkAccess.None;
        }
        else
        {
            IsOnline = access is not NetworkAccess.None;
        }

        bool wifi = false, cell = false;
        foreach (var p in profiles)
        {
            if (p == ConnectionProfile.WiFi) wifi = true;
            else if (p == ConnectionProfile.Cellular) cell = true;
        }
        IsWifi = wifi;
        IsCellular = cell;
    }

    public void Dispose()
        => Connectivity.Current.ConnectivityChanged -= OnChanged;
}
