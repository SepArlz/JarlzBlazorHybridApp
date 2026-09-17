using GabsHybridApp.Shared.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace GabsHybridApp.Maui.Services;

public sealed class MauiCameraService : ICameraService
{
    private const string PhotosFolderName = "Photos";

    public async Task<PhotoResult?> CapturePhotoAsync(CancellationToken ct = default)
    {
        // 1) Camera permission at runtime (Android)
        var cam = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (cam != PermissionStatus.Granted)
            cam = await Permissions.RequestAsync<Permissions.Camera>();
        if (cam != PermissionStatus.Granted)
            return null; // user denied

        // 2) Device supports capture? (emulators without a camera will return false)
        if (!MediaPicker.Default.IsCaptureSupported)
            return null;

        try
        {
            var file = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = $"photo_{DateTime.UtcNow:yyyyMMdd_HHmmss}"
            });

            if (file == null) return null;

            var path = await SaveToAppPhotosAsync(file, ct);
            return await BuildPhotoResultAsync(path, ct);
        }
        catch (FeatureNotSupportedException)
        {
            return null; // Camera not available
        }
        catch (PermissionException)
        {
            return null; // Permissions denied
        }
    }

    public async Task<PhotoResult?> BrowsePhotoAsync(CancellationToken ct = default)
    {
        // Runtime photo permission (Android 13+)
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            if (DeviceInfo.Version.Major >= 13)
                _ = await Permissions.RequestAsync<Permissions.Photos>();
            else
                _ = await Permissions.RequestAsync<Permissions.StorageRead>();
        }

        var photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
        {
            Title = "Select a photo"
        });

        var file = photos?.FirstOrDefault();
        if (file == null) return null;

        var path = await SaveToAppPhotosAsync(file, ct);
        return await BuildPhotoResultAsync(path, ct);
    }

    // ---------------------------
    // Internal helpers
    // ---------------------------
    private static async Task<string> SaveToAppPhotosAsync(FileResult file, CancellationToken ct)
    {
        var photosDir = Path.Combine(FileSystem.AppDataDirectory, PhotosFolderName);
        Directory.CreateDirectory(photosDir);

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";
        var finalName = $"photo_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}{ext}";
        var destination = Path.Combine(photosDir, finalName);

        using var src = await file.OpenReadAsync();
        using var dst = File.OpenWrite(destination);
        await src.CopyToAsync(dst, ct);

        return destination;
    }

    private static async Task<PhotoResult> BuildPhotoResultAsync(string path, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(path, ct);
        var mime = GetMimeFromExtension(Path.GetExtension(path));
        var fileName = Path.GetFileName(path);
        return new PhotoResult(Path: path, FileName: fileName, ContentType: mime, Bytes: bytes);
    }

    private static string GetMimeFromExtension(string? ext) => ext?.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };
}
