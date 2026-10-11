using System;

namespace GabsHybridApp.Shared.Models;

public enum ProductSortOption
{
    Id,
    Name,
    Price,
    PopularityToday,
    PopularityThisWeek,
    PopularityThisMonth,
    PopularityThisYear
}

public class ProductFilterDto
{
    public string? SearchTerm { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? SelectedUnit { get; set; }
    public ProductSortOption SortBy { get; set; } = ProductSortOption.Id;
    public bool SortDescending { get; set; } = false;

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchTerm) ||
        MinPrice.HasValue ||
        MaxPrice.HasValue ||
        !string.IsNullOrWhiteSpace(SelectedUnit);
}

public class ProductViewItem
{
    public Product Product { get; set; } = new();

    public int SalesToday { get; set; }
    public int SalesThisWeek { get; set; }
    public int SalesThisMonth { get; set; }
    public int SalesThisYear { get; set; }

    public int GetPopularityCount(ProductSortOption sort) => sort switch
    {
        ProductSortOption.PopularityToday => SalesToday,
        ProductSortOption.PopularityThisWeek => SalesThisWeek,
        ProductSortOption.PopularityThisMonth => SalesThisMonth,
        ProductSortOption.PopularityThisYear => SalesThisYear,
        _ => SalesThisYear
    };
}

public class ProductSummaryDto
{
    public int TotalProducts { get; set; }
    public int TotalUnits { get; set; }
    public decimal AveragePrice { get; set; }
    public string TopSellingProduct { get; set; } = "None";
    public int TopSellingCount { get; set; }
}
