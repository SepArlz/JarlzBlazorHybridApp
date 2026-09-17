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

public class OrderService : IOrderService
{
    private readonly IDbContextFactory<HybridAppDbContext> _dbFactory;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IDbContextFactory<HybridAppDbContext> dbFactory,
        ILogger<OrderService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<(List<Order> Items, int TotalCount)> GetOrdersAsync(OrderFilterDto filter, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var query = db.Orders
            .Include(o => o.Items)
            .Where(o => !o.IsDeleted)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(o =>
                o.OrderNumber.ToLower().Contains(term) ||
                o.CustomerName.ToLower().Contains(term) ||
                (o.CustomerEmail != null && o.CustomerEmail.ToLower().Contains(term)) ||
                (o.CustomerPhone != null && o.CustomerPhone.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(o => o.Status == filter.Status);
        }

        if (!string.IsNullOrWhiteSpace(filter.PaymentStatus))
        {
            query = query.Where(o => o.PaymentStatus == filter.PaymentStatus);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(o => o.OrderDateUtc >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(o => o.OrderDateUtc <= filter.ToDate.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(o => o.OrderDateUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<Order?> GetOrderByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct);
    }

    public async Task<OrderSummaryDto> GetOrderSummaryAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var today = DateTime.UtcNow.Date;

        var orders = await db.Orders
            .Where(o => !o.IsDeleted)
            .Select(o => new { o.Status, o.TotalAmount, o.OrderDateUtc })
            .ToListAsync(ct);

        return new OrderSummaryDto
        {
            TotalOrders = orders.Count,
            PendingOrders = orders.Count(o => o.Status == "Pending"),
            ProcessingOrders = orders.Count(o => o.Status == "Processing"),
            ShippedOrders = orders.Count(o => o.Status == "Shipped"),
            DeliveredOrders = orders.Count(o => o.Status == "Delivered"),
            CancelledOrders = orders.Count(o => o.Status == "Cancelled"),
            TotalRevenue = orders.Where(o => o.Status != "Cancelled").Sum(o => o.TotalAmount),
            TodayRevenue = orders.Where(o => o.Status != "Cancelled" && o.OrderDateUtc.Date == today).Sum(o => o.TotalAmount)
        };
    }

    public async Task<Order> CreateOrderAsync(Order order, CancellationToken ct = default)
    {
        if (order.Id == Guid.Empty)
        {
            order.Id = Guid.CreateVersion7();
        }

        if (string.IsNullOrWhiteSpace(order.OrderNumber))
        {
            order.OrderNumber = await GenerateOrderNumberAsync(ct);
        }

        RecalculateTotals(order);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Created order {OrderNumber} with {ItemCount} items, total: {Total:C}",
            order.OrderNumber, order.Items.Count, order.TotalAmount);

        return order;
    }

    public async Task<Order> UpdateOrderAsync(Order order, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var existing = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == order.Id && !o.IsDeleted, ct);

        if (existing == null)
        {
            throw new InvalidOperationException($"Order with id {order.Id} was not found.");
        }

        existing.CustomerName = order.CustomerName;
        existing.CustomerEmail = order.CustomerEmail;
        existing.CustomerPhone = order.CustomerPhone;
        existing.ShippingAddress = order.ShippingAddress;
        existing.BillingAddress = order.BillingAddress;
        existing.Status = order.Status;
        existing.PaymentStatus = order.PaymentStatus;
        existing.PaymentMethod = order.PaymentMethod;
        existing.ShippedDateUtc = order.ShippedDateUtc;
        existing.DeliveredDateUtc = order.DeliveredDateUtc;
        existing.Notes = order.Notes;
        existing.DiscountAmount = order.DiscountAmount;
        existing.TaxAmount = order.TaxAmount;
        existing.ShippingFee = order.ShippingFee;

        // Sync items
        db.OrderItems.RemoveRange(existing.Items);
        foreach (var item in order.Items)
        {
            if (item.Id == Guid.Empty) item.Id = Guid.CreateVersion7();
            item.OrderId = existing.Id;
            item.TotalPrice = (item.UnitPrice * item.Quantity) - item.Discount;
            existing.Items.Add(item);
        }

        RecalculateTotals(existing);

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Updated order {OrderNumber}", existing.OrderNumber);

        return existing;
    }

    public async Task<bool> UpdateOrderStatusAsync(Guid orderId, string status, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, ct);
        if (order == null) return false;

        order.Status = status;
        if (status == "Shipped" && !order.ShippedDateUtc.HasValue)
        {
            order.ShippedDateUtc = DateTime.UtcNow;
        }
        else if (status == "Delivered" && !order.DeliveredDateUtc.HasValue)
        {
            order.DeliveredDateUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order == null) return false;

        order.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Soft-deleted order {OrderNumber}", order.OrderNumber);
        return true;
    }

    public async Task<string> GenerateOrderNumberAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"ORD-{today}-";

        var count = await db.Orders
            .Where(o => o.OrderNumber.StartsWith(prefix))
            .CountAsync(ct);

        return $"{prefix}{(count + 1):D4}";
    }

    public async Task<List<Order>> GetRecentOrdersAsync(int count = 5, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Orders
            .Include(o => o.Items)
            .Where(o => !o.IsDeleted)
            .OrderByDescending(o => o.OrderDateUtc)
            .Take(count)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    private static void RecalculateTotals(Order order)
    {
        foreach (var item in order.Items)
        {
            item.TotalPrice = Math.Max(0, (item.UnitPrice * item.Quantity) - item.Discount);
        }

        order.SubTotal = order.Items.Sum(i => i.TotalPrice);
        order.TotalAmount = Math.Max(0, order.SubTotal - order.DiscountAmount + order.TaxAmount + order.ShippingFee);
    }
}
