using GabsHybridApp.Shared;
using GabsHybridApp.Shared.Data;
using GabsHybridApp.Shared.Services;
using GabsHybridApp.Web.Components;
using GabsHybridApp.Web.Endpoints;
using GabsHybridApp.Web.Extensions;
using GabsHybridApp.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSharedCore();

builder.Services.AddHttpContextAccessor();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o => { o.LoginPath = "/account/login"; o.LogoutPath = "/account/logout"; })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, opt =>
    {
        opt.RequireHttpsMetadata = true;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.Zero
        };
    });

// Authorization
builder.Services.AddAuthorization(options =>
{
    // Only API JWT (Bearer) + role MobileSync can access sync endpoints
    options.AddPolicy("SyncAccess", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("MobileSync");
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
    });
});

builder.Services.AddCascadingAuthenticationState();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var schema = builder.Configuration.GetConnectionString("Schema");
var isPostgres = connectionString?.Contains("Host=", StringComparison.OrdinalIgnoreCase) == true ||
                 connectionString?.Contains("Port=", StringComparison.OrdinalIgnoreCase) == true;

if (isPostgres)
{
    AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
}

builder.Services.AddDbContextFactory<HybridAppDbContext>(option =>
{
    if (isPostgres)
    {
        option.UseNpgsql(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(WebDesignTimeFactory).Assembly.GetName().Name);
            if (!string.IsNullOrWhiteSpace(schema))
                sql.MigrationsHistoryTable("__EFMigrationsHistory", schema);
            sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
        });
    }
    else
    {
        option.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(WebDesignTimeFactory).Assembly.GetName().Name);
            if (!string.IsNullOrWhiteSpace(schema)) sql.MigrationsHistoryTable("__EFMigrationsHistory", schema);
        });
    }
});


// Add device-specific services used by the GabsHybridApp.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddScoped<IHostCapabilities, WebHostCapabilities>();
builder.Services.AddScoped<IAuthService, ServerCookieAuthService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ILocationService, WebLocationService>();
builder.Services.AddScoped<ICameraService, WebCameraService>();
builder.Services.AddSingleton<IFlashlightService, NullFlashlightService>();
builder.Services.AddSingleton<INetworkService, NullNetworkService>();
builder.Services.AddSingleton<IBackupStorageProvider, WebBackupStorageProvider>();
builder.Services.AddSingleton<IAppUpdateService, WebAppUpdateService>();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<INonceCache, MemoryNonceCache>();
builder.Services.AddSingleton<ApiJwtIssuer>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    AppVersionInfo.PrewarmProductionPipeline(true);
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.MigrateDb<HybridAppDbContext>(true, seed: db =>
{
    // Absolute Guardrail: ZERO seeding in production under any circumstances
    if (app.Environment.IsProduction())
    {
        return;
    }

    db.SeedOrderData();
});

app.MapStaticAssets();

var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".apk"] = "application/vnd.android.package-archive";
contentTypeProvider.Mappings[".zip"] = "application/zip";
contentTypeProvider.Mappings[".exe"] = "application/octet-stream";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider
});

app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

// 📱 Offline App Download Endpoints
app.MapGet("/download/android", (IWebHostEnvironment env) =>
{
    var candidates = new[]
    {
        Path.Combine(env.WebRootPath ?? string.Empty, "_content", "GabsHybridApp.Shared", "releases"),
        Path.Combine(env.ContentRootPath, "wwwroot", "_content", "GabsHybridApp.Shared", "releases"),
        Path.Combine(env.ContentRootPath, "..", "GabsHybridApp.Shared", "wwwroot", "releases"),
        Path.Combine(env.ContentRootPath, "..", "..", "_releases")
    };

    foreach (var dir in candidates)
    {
        if (Directory.Exists(dir))
        {
            var apk = Directory.GetFiles(dir, "*.apk").OrderByDescending(f => File.GetLastWriteTimeUtc(f)).FirstOrDefault();
            if (apk != null)
            {
                return Results.File(apk, "application/vnd.android.package-archive", Path.GetFileName(apk));
            }
        }
    }
    return Results.NotFound("Android APK release not found.");
}).AllowAnonymous();

app.MapGet("/download/windows", (IWebHostEnvironment env) =>
{
    var candidates = new[]
    {
        Path.Combine(env.WebRootPath ?? string.Empty, "_content", "GabsHybridApp.Shared", "releases"),
        Path.Combine(env.ContentRootPath, "wwwroot", "_content", "GabsHybridApp.Shared", "releases"),
        Path.Combine(env.ContentRootPath, "..", "GabsHybridApp.Shared", "wwwroot", "releases"),
        Path.Combine(env.ContentRootPath, "..", "..", "_releases")
    };

    foreach (var dir in candidates)
    {
        if (Directory.Exists(dir))
        {
            var zip = Directory.GetFiles(dir, "*.zip").OrderByDescending(f => File.GetLastWriteTimeUtc(f)).FirstOrDefault();
            if (zip != null)
            {
                return Results.File(zip, "application/zip", Path.GetFileName(zip));
            }
        }
    }
    return Results.NotFound("Windows release zip not found.");
}).AllowAnonymous();

app.MapGet("/download/windows-exe", (IWebHostEnvironment env) =>
{
    var candidates = new[]
    {
        Path.Combine(env.WebRootPath ?? string.Empty, "_content", "GabsHybridApp.Shared", "releases"),
        Path.Combine(env.ContentRootPath, "wwwroot", "_content", "GabsHybridApp.Shared", "releases"),
        Path.Combine(env.ContentRootPath, "..", "GabsHybridApp.Shared", "wwwroot", "releases"),
        Path.Combine(env.ContentRootPath, "..", "..", "_releases")
    };

    foreach (var dir in candidates)
    {
        if (Directory.Exists(dir))
        {
            var exe = Directory.GetFiles(dir, "*.exe").OrderByDescending(f => File.GetLastWriteTimeUtc(f)).FirstOrDefault();
            if (exe != null)
            {
                return Results.File(exe, "application/vnd.microsoft.portable-executable", Path.GetFileName(exe));
            }
        }
    }
    return Results.NotFound("Windows release executable not found.");
}).AllowAnonymous();

// 🔔 Client In-App Update Manifest Endpoint
app.MapGet("/api/app-version", (IWebHostEnvironment env) =>
{
    var candidates = new[]
    {
        Path.Combine(env.WebRootPath ?? string.Empty, "_content", "GabsHybridApp.Shared", "releases"),
        Path.Combine(env.ContentRootPath, "wwwroot", "_content", "GabsHybridApp.Shared", "releases"),
        Path.Combine(env.ContentRootPath, "..", "GabsHybridApp.Shared", "wwwroot", "releases"),
        Path.Combine(env.ContentRootPath, "..", "..", "_releases")
    };

    // 1. Check for version.json emitted by publish-app.bat
    foreach (var dir in candidates)
    {
        if (Directory.Exists(dir))
        {
            var jsonPath = Path.Combine(dir, "version.json");
            if (File.Exists(jsonPath))
            {
                try
                {
                    var jsonContent = File.ReadAllText(jsonPath);
                    var manifest = System.Text.Json.JsonSerializer.Deserialize<GabsHybridApp.Shared.Models.AppVersionManifest>(
                        jsonContent,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (manifest != null)
                    {
                        PopulateFileSizes(manifest, dir);
                        return Results.Ok(manifest);
                    }
                }
                catch { }
            }
        }
    }

    // 2. Fallback: discover from newest release binaries or runtime assembly
    var fallbackManifest = new GabsHybridApp.Shared.Models.AppVersionManifest
    {
        Version = AppVersionInfo.Version,
        ReleaseNotes = "Standard release build with system stability and performance enhancements.",
        ReleaseDateUtc = DateTime.UtcNow
    };

    foreach (var dir in candidates)
    {
        if (Directory.Exists(dir))
        {
            PopulateFileSizes(fallbackManifest, dir);
            if (fallbackManifest.AndroidSizeBytes > 0 || fallbackManifest.WindowsSizeBytes > 0)
                break;
        }
    }

    return Results.Ok(fallbackManifest);

    static void PopulateFileSizes(GabsHybridApp.Shared.Models.AppVersionManifest manifest, string dir)
    {
        var apk = Directory.GetFiles(dir, "*.apk").OrderByDescending(f => f).FirstOrDefault();
        if (apk != null && File.Exists(apk))
        {
            manifest.AndroidSizeBytes = new FileInfo(apk).Length;
            if (string.IsNullOrEmpty(manifest.Version) || manifest.Version == "1.0.0")
            {
                var match = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(apk), @"v?(\d+\.\d+\.\d+)");
                if (match.Success) manifest.Version = match.Groups[1].Value;
            }
        }

        var exe = Directory.GetFiles(dir, "*.exe").OrderByDescending(f => f).FirstOrDefault();
        if (exe != null && File.Exists(exe))
        {
            manifest.WindowsSizeBytes = new FileInfo(exe).Length;
        }
        else
        {
            var zip = Directory.GetFiles(dir, "*.zip").OrderByDescending(f => f).FirstOrDefault();
            if (zip != null && File.Exists(zip))
            {
                manifest.WindowsSizeBytes = new FileInfo(zip).Length;
            }
        }
    }
}).AllowAnonymous();

// 📦 Database Backup Download Endpoints (Web Admin & Authenticated MAUI Clients)
app.MapGet("/download-db", async (HttpContext ctx, IDataBackupService backupService) =>
{
    var user = ctx.User;
    bool isAuthorized = user.Identity?.IsAuthenticated == true;

    if (!isAuthorized)
    {
        return Results.Unauthorized();
    }

    var filePath = await backupService.GetLatestBackupFilePathAsync();
    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
    {
        var adminName = user.Identity?.Name ?? "Admin";
        var record = await backupService.CreateBackupAsync(adminName, "ENDPOINT", "Generated via /download-db endpoint");
        filePath = await backupService.GetBackupFilePathAsync(record.Id);
    }

    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
    {
        return Results.NotFound("No database backup archive available.");
    }

    return Results.File(filePath, "application/zip", Path.GetFileName(filePath));
})
.RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { AuthenticationSchemes = $"{CookieAuthenticationDefaults.AuthenticationScheme},{JwtBearerDefaults.AuthenticationScheme}" });

app.MapGet("/download-db/{id:guid}", async (Guid id, HttpContext ctx, IDataBackupService backupService) =>
{
    var user = ctx.User;
    bool isAuthorized = user.Identity?.IsAuthenticated == true;

    if (!isAuthorized)
    {
        return Results.Unauthorized();
    }

    var filePath = await backupService.GetBackupFilePathAsync(id);
    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
    {
        return Results.NotFound($"Backup archive '{id}' was not found on disk.");
    }

    return Results.File(filePath, "application/zip", Path.GetFileName(filePath));
})
.RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { AuthenticationSchemes = $"{CookieAuthenticationDefaults.AuthenticationScheme},{JwtBearerDefaults.AuthenticationScheme}" });

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(GabsHybridApp.Shared._Imports).Assembly);

app.MapPost("/_internal/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/account/login");
}).AllowAnonymous().DisableAntiforgery();

app.MapAuthExchangeEndpoints();
app.MapSyncEndpoints();

app.MapHub<NotificationHub>("/notificationhub");

app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api") && !ctx.Request.Path.StartsWithSegments("/download") && !ctx.Request.Path.StartsWithSegments("/download-db"), then =>
{
    then.UseStatusCodePagesWithRedirects("/404");
});

app.Run();
