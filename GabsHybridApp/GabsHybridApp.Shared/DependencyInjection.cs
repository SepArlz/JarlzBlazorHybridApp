// GabsHybridApp.Shared/DependencyInjection.cs
using Blazored.LocalStorage;
using BlazorState;
using GabsHybridApp.Shared.Services;
using GabsHybridApp.Shared.States;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using MudExtensions.Services;
using System.Reflection;

namespace GabsHybridApp.Shared;

public static class DependencyInjection
{
    public static IServiceCollection AddSharedCore(this IServiceCollection services, Assembly? statesAssembly = null)
    {
        services.AddOptions();

        services.AddBlazoredLocalStorage();
        services.AddBlazorState(opts => opts.Assemblies = new[] { typeof(CounterState).GetTypeInfo().Assembly });

        // Auth state provider (used by both hosts)
        services.AddAuthorizationCore(); // not AddAuthorization()
        services.AddScoped<HostedAuthStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<HostedAuthStateProvider>());
        services.AddScoped<HmacAuthTokenProvider>();

        // App services that are host-agnostic
        services.AddScoped<UserService>();
        services.AddScoped<ProductSyncService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IAppThemeService, DefaultAppThemeService>();
        services.AddSingleton<IDeviceIdProvider, DefaultDeviceIdProvider>();
        services.AddSingleton<IBackupStorageProvider, DefaultBackupStorageProvider>();
        services.AddSingleton<GabsHybridApp.Shared.Services.ModelAdapter.SchemaIntrospectionService>();
        services.AddSingleton<GabsHybridApp.Shared.Services.ModelAdapter.AdaptiveTableReader>();
        services.AddScoped<IDataBackupService, DataBackupService>();

        // UI libs
        services.AddMudServices();
        services.AddMudExtensions();

        return services;
    }
}
