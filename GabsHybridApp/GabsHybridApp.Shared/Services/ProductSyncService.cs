using GabsHybridApp.Shared.Data;
using GabsHybridApp.Shared.Models;
using GabsHybridApp.Shared.States;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace GabsHybridApp.Shared.Services;

public sealed class ProductSyncService
{
    private readonly IDbContextFactory<HybridAppDbContext> _dbFactory;
    private readonly HttpClient _http;
    private readonly IFormFactor _formFactor;
    private readonly HmacAuthTokenProvider _auth;

    public ProductSyncService(
        IDbContextFactory<HybridAppDbContext> dbFactory,
        HttpClient http,
        IFormFactor formFactor,
        HmacAuthTokenProvider auth)
    {
        _dbFactory = dbFactory;
        _http = http;
        _formFactor = formFactor;
        _auth = auth;
    }

    /// <summary>
    /// Pull-all + AddRange; skip by name; swallow errors. No deletes, no versioning.
    /// </summary>
    public async Task<int> SyncAsync(string username, CancellationToken ct = default)
    {
        // Only perform sync on MAUI/WinUI (not on Web host)
        if (string.Equals(_formFactor.GetFormFactor(), "Web", StringComparison.OrdinalIgnoreCase))
            return 0;

        var baseUrl = StorageConstants.AppWebUrl.TrimEnd('/');
        var deviceId = _formFactor.GetFormFactor(); // simple for now

        // --- Ensure Bearer token; guard network failures ---
        string? token = null;
        try
        {
            token = await _auth.EnsureAccessTokenAsync(baseUrl, username, deviceId, ct);
        }
        catch
        {
            // URL unreachable / DNS / timeout / HttpRequestException
            return 0;
        }
        if (string.IsNullOrEmpty(token))
            return 0;

        // --- Download products (retry once on 401) ---
        var list = await DownloadProductsAsync(baseUrl, ct);
        if (list is null)
        {
            // clear + retry BUT guard token call again
            _auth.Clear();

            try
            {
                token = await _auth.EnsureAccessTokenAsync(baseUrl, username, deviceId, ct);
            }
            catch
            {
                return 0;
            }
            if (string.IsNullOrEmpty(token))
                return 0;

            list = await DownloadProductsAsync(baseUrl, ct);
            if (list is null) return 0;
        }

        // Nothing to apply
        if (list.Count == 0) return 0;

        // --- Apply locally (skip existing names, case-insensitive) ---
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var existingNames = new HashSet<string>(
                await db.Products.AsNoTracking()
                    .Where(x => x.Name != null && x.Name != "")
                    .Select(x => x.Name!.Trim())
                    .ToListAsync(ct),
                StringComparer.OrdinalIgnoreCase);

            var toInsert = new List<Product>(list.Count);
            foreach (var p in list)
            {
                var name = p.Name?.Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!existingNames.Add(name)) continue; // skip dups

                toInsert.Add(new Product
                {
                    Name = name,
                    Description = p.Description,
                    UnitPrice = p.UnitPrice,
                    Unit = p.Unit,
                    PictureFilename = p.PictureFilename
                });
            }

            if (toInsert.Count == 0) return 0;

            try
            {
                await db.Products.AddRangeAsync(toInsert, ct);
                return await db.SaveChangesAsync(ct);
            }
            catch
            {
                // Fallback per-row; swallow conflicts
                var applied = 0;
                foreach (var p in toInsert)
                {
                    try
                    {
                        await db.Products.AddAsync(p, ct);
                        applied += await db.SaveChangesAsync(ct);
                    }
                    catch { /* swallow */ }
                }
                return applied;
            }
        }
        catch
        {
            // swallow DB factory/context issues
            return 0;
        }
    }

    private async Task<List<Product>?> DownloadProductsAsync(string baseUrl, CancellationToken ct)
    {
        try
        {
            var url = $"{baseUrl}/api/sync/products/all";
            var resp = await _http.GetAsync(url, ct);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return null; // signal retry

            if (!resp.IsSuccessStatusCode)
                return new List<Product>(); // treat as empty

            return await resp.Content.ReadFromJsonAsync<List<Product>>(cancellationToken: ct)
                   ?? new List<Product>();
        }
        catch
        {
            // swallow network/JSON failures
            return new List<Product>();
        }
    }

}
