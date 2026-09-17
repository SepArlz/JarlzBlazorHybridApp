using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GabsHybridApp.Shared.Models;

namespace GabsHybridApp.Shared.Services;

public interface IOrderService
{
    Task<(List<Order> Items, int TotalCount)> GetOrdersAsync(OrderFilterDto filter, CancellationToken ct = default);
    Task<Order?> GetOrderByIdAsync(Guid id, CancellationToken ct = default);
    Task<OrderSummaryDto> GetOrderSummaryAsync(CancellationToken ct = default);
    Task<Order> CreateOrderAsync(Order order, CancellationToken ct = default);
    Task<Order> UpdateOrderAsync(Order order, CancellationToken ct = default);
    Task<bool> UpdateOrderStatusAsync(Guid orderId, string status, CancellationToken ct = default);
    Task<bool> DeleteOrderAsync(Guid orderId, CancellationToken ct = default);
    Task<string> GenerateOrderNumberAsync(CancellationToken ct = default);
    Task<List<Order>> GetRecentOrdersAsync(int count = 5, CancellationToken ct = default);
}
