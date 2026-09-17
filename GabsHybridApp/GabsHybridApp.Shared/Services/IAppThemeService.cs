namespace GabsHybridApp.Shared.Services;

public interface IAppThemeService
{
    Task SetThemeAsync(bool isDarkMode);
}

public class DefaultAppThemeService : IAppThemeService
{
    public Task SetThemeAsync(bool isDarkMode) => Task.CompletedTask;
}
