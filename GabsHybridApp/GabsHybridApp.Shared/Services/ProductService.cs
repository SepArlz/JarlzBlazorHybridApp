using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GabsHybridApp.Shared.Data;
using GabsHybridApp.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GabsHybridApp.Shared.Services;

public class ProductService : IProductService
{
    private readonly IDbContextFactory<HybridAppDbContext> _dbFactory;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        IDbContextFactory<HybridAppDbContext> dbFactory,
        ILogger<ProductService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<List<ProductViewItem>> GetProductsAsync(ProductFilterDto filter, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var products = await db.Products.AsNoTracking().ToListAsync(ct);

        var todayUtc = DateTime.UtcNow.Date;
        var startOfWeek = todayUtc.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfYear = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Fetch sales records within this calendar year
        var salesStats = await db.OrderItems
            .AsNoTracking()
            .Where(oi => oi.ProductId != null && oi.Order != null && !oi.Order.IsDeleted && oi.Order.OrderDateUtc >= startOfYear)
            .Select(oi => new { oi.ProductId, oi.Quantity, oi.Order!.OrderDateUtc })
            .ToListAsync(ct);

        var salesByProduct = salesStats
            .GroupBy(x => x.ProductId!.Value)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Today = g.Where(x => x.OrderDateUtc >= todayUtc).Sum(x => x.Quantity),
                    Week = g.Where(x => x.OrderDateUtc >= startOfWeek).Sum(x => x.Quantity),
                    Month = g.Where(x => x.OrderDateUtc >= startOfMonth).Sum(x => x.Quantity),
                    Year = g.Sum(x => x.Quantity)
                });

        var viewItems = products.Select(p =>
        {
            salesByProduct.TryGetValue(p.Id, out var sales);
            return new ProductViewItem
            {
                Product = p,
                SalesToday = sales?.Today ?? 0,
                SalesThisWeek = sales?.Week ?? 0,
                SalesThisMonth = sales?.Month ?? 0,
                SalesThisYear = sales?.Year ?? 0
            };
        }).ToList();

        // 1. Search filter: Strict name-only search for high accuracy
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            viewItems = viewItems.Where(item =>
                item.Product.Name != null && item.Product.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        // 2. Unit filter
        if (!string.IsNullOrWhiteSpace(filter.SelectedUnit))
        {
            viewItems = viewItems.Where(item =>
                string.Equals(item.Product.Unit, filter.SelectedUnit, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        // 3. Price range filter
        if (filter.MinPrice.HasValue)
        {
            viewItems = viewItems.Where(item => item.Product.UnitPrice >= filter.MinPrice.Value).ToList();
        }

        if (filter.MaxPrice.HasValue)
        {
            viewItems = viewItems.Where(item => item.Product.UnitPrice <= filter.MaxPrice.Value).ToList();
        }

        // 4. Sorting engine
        IEnumerable<ProductViewItem> sorted = filter.SortBy switch
        {
            ProductSortOption.Id => filter.SortDescending
                ? viewItems.OrderByDescending(x => x.Product.Id)
                : viewItems.OrderBy(x => x.Product.Id),

            ProductSortOption.Name => filter.SortDescending
                ? viewItems.OrderByDescending(x => x.Product.Name)
                : viewItems.OrderBy(x => x.Product.Name),

            ProductSortOption.Price => filter.SortDescending
                ? viewItems.OrderByDescending(x => x.Product.UnitPrice)
                : viewItems.OrderBy(x => x.Product.UnitPrice),

            ProductSortOption.PopularityToday => filter.SortDescending
                ? viewItems.OrderBy(x => x.SalesToday).ThenBy(x => x.Product.Name)
                : viewItems.OrderByDescending(x => x.SalesToday).ThenBy(x => x.Product.Name),

            ProductSortOption.PopularityThisWeek => filter.SortDescending
                ? viewItems.OrderBy(x => x.SalesThisWeek).ThenBy(x => x.Product.Name)
                : viewItems.OrderByDescending(x => x.SalesThisWeek).ThenBy(x => x.Product.Name),

            ProductSortOption.PopularityThisMonth => filter.SortDescending
                ? viewItems.OrderBy(x => x.SalesThisMonth).ThenBy(x => x.Product.Name)
                : viewItems.OrderByDescending(x => x.SalesThisMonth).ThenBy(x => x.Product.Name),

            ProductSortOption.PopularityThisYear => filter.SortDescending
                ? viewItems.OrderBy(x => x.SalesThisYear).ThenBy(x => x.Product.Name)
                : viewItems.OrderByDescending(x => x.SalesThisYear).ThenBy(x => x.Product.Name),

            _ => viewItems.OrderBy(x => x.Product.Name)
        };

        return sorted.ToList();
    }

    public async Task<List<string>> GetDistinctUnitsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Products
            .AsNoTracking()
            .Where(p => !string.IsNullOrWhiteSpace(p.Unit))
            .Select(p => p.Unit!)
            .Distinct()
            .OrderBy(u => u)
            .ToListAsync(ct);
    }

    public async Task<ProductSummaryDto> GetProductSummaryAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var products = await db.Products.AsNoTracking().ToListAsync(ct);

        if (products.Count == 0)
        {
            return new ProductSummaryDto();
        }

        var unitsCount = products
            .Where(p => !string.IsNullOrWhiteSpace(p.Unit))
            .Select(p => p.Unit!.Trim().ToLowerInvariant())
            .Distinct()
            .Count();

        var avgPrice = products.Average(p => p.UnitPrice);

        // Find top-selling product overall
        var topSales = await db.OrderItems
            .AsNoTracking()
            .Where(oi => oi.ProductId != null && oi.Order != null && !oi.Order.IsDeleted)
            .GroupBy(oi => oi.ProductId!.Value)
            .Select(g => new { ProductId = g.Key, TotalSold = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.TotalSold)
            .FirstOrDefaultAsync(ct);

        var topProduct = topSales != null
            ? products.FirstOrDefault(p => p.Id == topSales.ProductId)
            : null;

        return new ProductSummaryDto
        {
            TotalProducts = products.Count,
            TotalUnits = unitsCount,
            AveragePrice = avgPrice,
            TopSellingProduct = topProduct?.Name ?? "None",
            TopSellingCount = topSales?.TotalSold ?? 0
        };
    }

    public async Task<int> GetNextProductIdAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var maxId = await db.Products.AsNoTracking().MaxAsync(p => (int?)p.Id, ct) ?? 0;
        return maxId + 1;
    }

    public async Task<Product> CreateProductAsync(Product product, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        product.Id = 0; // Let database identity assign the new auto-increment ID
        await db.Products.AddAsync(product, ct);
        await db.SaveChangesAsync(ct);
        return product;
    }

    public async Task<Product> UpdateProductAsync(Product product, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var existing = await db.Products.FirstOrDefaultAsync(p => p.Id == product.Id, ct);
        if (existing == null)
        {
            throw new KeyNotFoundException($"Product with ID #{product.Id} not found.");
        }

        existing.Name = product.Name;
        existing.UnitPrice = product.UnitPrice;
        existing.Unit = product.Unit;
        existing.Description = product.Description;
        existing.PictureFilename = product.PictureFilename;

        await db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteProductAsync(int productId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var existing = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (existing == null) return false;

        db.Products.Remove(existing);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
