using System.Threading.Tasks;
using GabsHybridApp.Shared.Services;

namespace GabsHybridApp.Maui.Services;

public class MauiAppThemeService : IAppThemeService
{
    public Task SetThemeAsync(bool isDarkMode)
    {
        StatusBarThemeExtensions.ApplyTheme(isDarkMode);
        return Task.CompletedTask;
    }
}
