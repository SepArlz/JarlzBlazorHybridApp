using System.Linq;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using GabsHybridApp.Shared.Services;

namespace GabsHybridApp.Maui.Services;

public class MauiDeviceIdProvider : IDeviceIdProvider
{
    private const string PersistentDeviceIdKey = "persistent_device_id";
    private static string? _cachedId;

    public string GetDeviceId()
    {
        if (!string.IsNullOrEmpty(_cachedId))
            return _cachedId;

        _cachedId = Preferences.Default.Get(PersistentDeviceIdKey, string.Empty);
        if (string.IsNullOrWhiteSpace(_cachedId))
        {
            var model = DeviceInfo.Current.Model;
            var cleanModel = string.IsNullOrWhiteSpace(model)
                ? "MAUI"
                : new string(model.Where(char.IsLetterOrDigit).ToArray()).ToUpper();
            var prefix = cleanModel.Length > 6 ? cleanModel[..6] : (cleanModel.Length > 0 ? cleanModel : "MAUI");
            _cachedId = $"{prefix}-{Guid.NewGuid():N}"[..12].ToUpper();
            Preferences.Default.Set(PersistentDeviceIdKey, _cachedId);
        }

        return _cachedId;
    }
}
