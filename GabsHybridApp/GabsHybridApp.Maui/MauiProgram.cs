using CommunityToolkit.Maui;
using GabsHybridApp.Maui.Extensions;
using GabsHybridApp.Maui.Services;
using GabsHybridApp.Shared;
using GabsHybridApp.Shared.Data;
using GabsHybridApp.Shared.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GabsHybridApp.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddSharedCore();

        builder.Services.AddPooledDbContextFactory<HybridAppDbContext>(opt =>
            opt.UseSqlite("Data Source=hybrid_mauiDb.db", FileSystem.AppDataDirectory));

        // Add device-specific services used by the GabsHybridApp.Shared project
        builder.Services.AddSingleton<IFormFactor, FormFactor>();
        builder.Services.AddScoped<IHostCapabilities, MauiHostCapabilities>();
        builder.Services.AddScoped<IAppThemeService, MauiAppThemeService>();
        builder.UseStatusBarTheme("#2979ff", lightContent: true);

        builder.Services.AddMauiBlazorWebView();

        builder.UseMauiApp<App>().UseMauiCommunityToolkit().UseMauiCommunityToolkitCamera();

        builder.Services.AddScoped<IAuthService, MauiLocalAuthService>();
        builder.Services.AddScoped<INotificationService, NotificationService>();
        builder.Services.AddScoped<ILocationService, MauiLocationService>();
        builder.Services.AddSingleton<ICameraService, MauiCameraService>();
        builder.Services.AddSingleton<IFlashlightService, MauiFlashlightService>();
        builder.Services.AddSingleton<INetworkService, MauiNetworkService>();
        builder.Services.AddSingleton<IDeviceIdProvider, MauiDeviceIdProvider>();
        builder.Services.AddSingleton<IBackupStorageProvider, MauiBackupStorageProvider>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // ✨ One-liner: migrate DB (and optionally seed) on startup; WAL enabled for mobile.
        app.MigrateDb<HybridAppDbContext>(enableWal: true, seed: db =>
        {
            db.SeedOrderData();
        });

        return app;
    }
}
