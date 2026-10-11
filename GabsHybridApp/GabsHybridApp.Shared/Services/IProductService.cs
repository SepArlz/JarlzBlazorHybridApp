using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GabsHybridApp.Shared.Models;

namespace GabsHybridApp.Shared.Services;

public interface IProductService
{
    Task<List<ProductViewItem>> GetProductsAsync(ProductFilterDto filter, CancellationToken ct = default);
    Task<List<string>> GetDistinctUnitsAsync(CancellationToken ct = default);
    Task<ProductSummaryDto> GetProductSummaryAsync(CancellationToken ct = default);
    Task<int> GetNextProductIdAsync(CancellationToken ct = default);
    Task<Product> CreateProductAsync(Product product, CancellationToken ct = default);
    Task<Product> UpdateProductAsync(Product product, CancellationToken ct = default);
    Task<bool> DeleteProductAsync(int productId, CancellationToken ct = default);
}
