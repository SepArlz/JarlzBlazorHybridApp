using System;
using System.Collections.Generic;
using System.Linq;
using GabsHybridApp.Shared.Models;

namespace GabsHybridApp.Shared.Data;

public static class OrderDataSeeder
{
    public static void SeedOrderData(this HybridAppDbContext db)
    {
        if (db.Orders.Any()) return;

        var orders = new List<Order>
        {
            // 1. Delivered Order
            new Order
            {
                Id = Guid.Parse("90000000-0000-0000-0000-000000000001"),
                OrderNumber = "ORD-20260901-1001",
                CustomerName = "Sarah Jenkins",
                CustomerEmail = "sarah.jenkins@example.com",
                CustomerPhone = "+63 917 555 0192",
                ShippingAddress = "142 Palm Drive, Greenfield Estates, Davao City",
                BillingAddress = "142 Palm Drive, Greenfield Estates, Davao City",
                Status = "Delivered",
                PaymentStatus = "Paid",
                PaymentMethod = "CreditCard",
                OrderDateUtc = new DateTime(2026, 9, 1, 9, 30, 0, DateTimeKind.Utc),
                ShippedDateUtc = new DateTime(2026, 9, 2, 14, 0, 0, DateTimeKind.Utc),
                DeliveredDateUtc = new DateTime(2026, 9, 4, 11, 15, 0, DateTimeKind.Utc),
                SubTotal = 1000.00m,
                DiscountAmount = 50.00m,
                TaxAmount = 114.00m,
                ShippingFee = 120.00m,
                TotalAmount = 1184.00m,
                Notes = "Please leave package at the front porch if unattended.",
                CreatedBy = "System",
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Id = Guid.Parse("91000000-0000-0000-0000-000000000001"),
                        ProductId = 1,
                        ProductName = "Tide Laundry Detergent",
                        ProductUnit = "bottle",
                        UnitPrice = 350.00m,
                        Quantity = 2,
                        Discount = 0.00m,
                        TotalPrice = 700.00m
                    },
                    new OrderItem
                    {
                        Id = Guid.Parse("91000000-0000-0000-0000-000000000002"),
                        ProductId = 4,
                        ProductName = "Dove Beauty Bar",
                        ProductUnit = "pack",
                        UnitPrice = 75.00m,
                        Quantity = 4,
                        Discount = 0.00m,
                        TotalPrice = 300.00m
                    }
                }
            },

            // 2. Shipped Order
            new Order
            {
                Id = Guid.Parse("90000000-0000-0000-0000-000000000002"),
                OrderNumber = "ORD-20260905-1002",
                CustomerName = "Marcus Aurelius Tan",
                CustomerEmail = "marcus.tan@enterprise.ph",
                CustomerPhone = "+63 920 888 4321",
                ShippingAddress = "Suite 804, Horizon Tower, Ayala Center, Cebu City",
                BillingAddress = "Suite 804, Horizon Tower, Ayala Center, Cebu City",
                Status = "Shipped",
                PaymentStatus = "Paid",
                PaymentMethod = "GCash",
                OrderDateUtc = new DateTime(2026, 9, 5, 14, 10, 0, DateTimeKind.Utc),
                ShippedDateUtc = new DateTime(2026, 9, 6, 8, 45, 0, DateTimeKind.Utc),
                SubTotal = 2010.00m,
                DiscountAmount = 100.00m,
                TaxAmount = 229.20m,
                ShippingFee = 150.00m,
                TotalAmount = 2289.20m,
                Notes = "Deliver between 9 AM and 5 PM office hours.",
                CreatedBy = "System",
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Id = Guid.Parse("91000000-0000-0000-0000-000000000003"),
                        ProductId = 7,
                        ProductName = "Nescafe Instant Coffee",
                        ProductUnit = "jar",
                        UnitPrice = 170.00m,
                        Quantity = 6,
                        Discount = 0.00m,
                        TotalPrice = 1020.00m
                    },
                    new OrderItem
                    {
                        Id = Guid.Parse("91000000-0000-0000-0000-000000000004"),
                        ProductId = 8,
                        ProductName = "KitKat Chocolate Bar",
                        ProductUnit = "bar",
                        UnitPrice = 30.00m,
                        Quantity = 15,
                        Discount = 0.00m,
                        TotalPrice = 450.00m
                    },
                    new OrderItem
                    {
                        Id = Guid.Parse("91000000-0000-0000-0000-000000000005"),
                        ProductId = 9,
                        ProductName = "Nestle Pure Life Water",
                        ProductUnit = "pack",
                        UnitPrice = 180.00m,
                        Quantity = 3,
                        Discount = 0.00m,
                        TotalPrice = 540.00m
                    }
                }
            },

            // 3. Processing Order
            new Order
            {
                Id = Guid.Parse("90000000-0000-0000-0000-000000000003"),
                OrderNumber = "ORD-20260910-1003",
                CustomerName = "Elena Rostova",
                CustomerEmail = "elena.rostova@gmail.com",
                CustomerPhone = "+63 918 333 7744",
                ShippingAddress = "Lot 12 Blk 4, Villa Solana, Cagayan de Oro City",
                BillingAddress = "Lot 12 Blk 4, Villa Solana, Cagayan de Oro City",
                Status = "Processing",
                PaymentStatus = "Paid",
                PaymentMethod = "BankTransfer",
                OrderDateUtc = new DateTime(2026, 9, 10, 11, 20, 0, DateTimeKind.Utc),
                SubTotal = 3776.50m,
                DiscountAmount = 150.00m,
                TaxAmount = 435.18m,
                ShippingFee = 180.00m,
                TotalAmount = 4241.68m,
                Notes = "Order verified and packed. Waiting for courier pickup.",
                CreatedBy = "admin@gabshybrid.com",
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Id = Guid.Parse("91000000-0000-0000-0000-000000000006"),
                        ProductId = 2,
                        ProductName = "Pampers Diapers",
                        ProductUnit = "box",
                        UnitPrice = 999.00m,
                        Quantity = 3,
                        Discount = 0.00m,
                        TotalPrice = 2997.00m
                    },
                    new OrderItem
                    {
                        Id = Guid.Parse("91000000-0000-0000-0000-000000000007"),
                        ProductId = 3,
                        ProductName = "Gillette Fusion Razor",
                        ProductUnit = "pack",
                        UnitPrice = 199.75m,
                        Quantity = 2,
                        Discount = 0.00m,
                        TotalPrice = 399.50m
                    },
                    new OrderItem
                    {
                        Id = Guid.Parse("91000000-0000-0000-0000-000000000008"),
                        ProductId = 6,
                        ProductName = "Axe Body Spray",
                        ProductUnit = "bottle",
                        UnitPrice = 190.00m,
                        Quantity = 2,
                        Discount = 0.00m,
                        TotalPrice = 380.00m
                    }
                }
            },

            // 4. Confirmed Order
            new Order
            {
                Id = Guid.Parse("90000000-0000-0000-0000-000000000004"),
                OrderNumber = "ORD-20260912-1004",
                CustomerName = "Carlos Gabriel Lopez",
                CustomerEmail = "carlos.lopez@outlook.com",
                CustomerPhone = "+63 939 123 9988",
                ShippingAddress = "77 Acacia St, Juna Subdivision, Matina, Davao City",
                BillingAddress = "77 Acacia St, Juna Subdivision, Matina, Davao City",
                Status = "Confirmed",
                PaymentStatus = "Pending",
                PaymentMethod = "CashOnDelivery",
                OrderDateUtc = new DateTime(2026, 9, 12, 16, 45, 0, DateTimeKind.Utc),
                SubTotal = 1606.38m,
                DiscountAmount = 0.00m,
                TaxAmount = 192.77m,
                ShippingFee = 100.00m,
                TotalAmount = 1899.15m,
                Notes = "COD verified by customer phone call.",
                CreatedBy = "admin@gabshybrid.com",
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Id = Guid.Parse("91000000-0000-0000-0000-000000000009"),
                        ProductId = 10,
                        ProductName = "Coca-Cola 1 Liter",
                        ProductUnit = "bottle",
                        UnitPrice = 30.00m,
                        Quantity = 12,
                        Discount = 0.00m,
                        TotalPrice = 360.00m
                    },
                    new OrderItem
                    {
                        Id = Guid.Parse("91000000-0000-0000-0000-000000000010"),
                        ProductId = 11,
                        ProductName = "Sprite 12 oz",
                        ProductUnit = "can",
                        UnitPrice = 12.00m,
                        Quantity = 24,
                        Discount = 0.00m,
                        TotalPrice = 288.00m
                    },
                    new OrderItem
                    {
                        Id = Guid.Parse("91000000-0000-0000-0000-000000000011"),
                        ProductId = 12,
                        ProductName = "Minute Maid Pulpy Orange",
                        ProductUnit = "bottle",
                        UnitPrice = 25.00m,
                        Quantity = 10,
                        Discount = 0.00m,
                        TotalPrice = 250.00m
                    },
                    new OrderItem
                    {
                        Id = Guid.Parse("91000000-0000-0000-0000-000000000012"),
                        ProductId = 14,
                        ProductName = "Red Horse Beer",
                        ProductUnit = "bottle",
                        UnitPrice = 118.0625m,
                        Quantity = 6,
                        Discount = 0.00m,
                        TotalPrice = 708.38m
                    }
                }
            },

            // 5. Pending Order
            new Order
            {
                Id = Guid.Parse("90000000-0000-0000-0000-000000000005"),
                OrderNumber = "ORD-20260915-1005",
                CustomerName = "Beatriz Morales",
                CustomerEmail = "beatriz.morales@yahoo.com",
                CustomerPhone = "+63 927 456 1122",
                ShippingAddress = "Block 8 Lot 15, Sunset Heights, Bukidnon",
                BillingAddress = "Block 8 Lot 15, Sunset Heights, Bukidnon",
                Status = "Pending",
                PaymentStatus = "Pending",
                PaymentMethod = "GCash",
                OrderDateUtc = new DateTime(2026, 9, 15, 10, 5, 0, DateTimeKind.Utc),
                SubTotal = 930.00m,
                DiscountAmount = 0.00m,
                TaxAmount = 111.60m,
                ShippingFee = 90.00m,
                TotalAmount = 1131.60m,
                Notes = "Awaiting customer GCash reference number confirmation.",
                CreatedBy = "System",
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Id = Guid.Parse("91000000-0000-0000-0000-000000000013"),
                        ProductId = 1,
                        ProductName = "Tide Laundry Detergent",
                        ProductUnit = "bottle",
                        UnitPrice = 350.00m,
                        Quantity = 1,
                        Discount = 0.00m,
                        TotalPrice = 350.00m
                    },
                    new OrderItem
                    {
                        Id = Guid.Parse("91000000-0000-0000-0000-000000000014"),
                        ProductId = 5,
                        ProductName = "Hellmann's Mayonnaise",
                        ProductUnit = "jar",
                        UnitPrice = 120.00m,
                        Quantity = 2,
                        Discount = 0.00m,
                        TotalPrice = 240.00m
                    },
                    new OrderItem
                    {
                        Id = Guid.Parse("91000000-0000-0000-0000-000000000015"),
                        ProductId = 7,
                        ProductName = "Nescafe Instant Coffee",
                        ProductUnit = "jar",
                        UnitPrice = 170.00m,
                        Quantity = 2,
                        Discount = 0.00m,
                        TotalPrice = 340.00m
                    }
                }
            },

            // 6. Cancelled Order
            new Order
            {
                Id = Guid.Parse("90000000-0000-0000-0000-000000000006"),
                OrderNumber = "ORD-20260916-1006",
                CustomerName = "Derrick Vanguard",
                CustomerEmail = "derrick.v@techcorp.io",
                CustomerPhone = "+63 905 678 1234",
                ShippingAddress = "Unit 12B, Pacific Crest Tower, Taguig City",
                BillingAddress = "Unit 12B, Pacific Crest Tower, Taguig City",
                Status = "Cancelled",
                PaymentStatus = "Refunded",
                PaymentMethod = "CreditCard",
                OrderDateUtc = new DateTime(2026, 9, 16, 13, 0, 0, DateTimeKind.Utc),
                SubTotal = 1163.27m,
                DiscountAmount = 50.00m,
                TaxAmount = 133.59m,
                ShippingFee = 120.00m,
                TotalAmount = 1366.86m,
                Notes = "Customer requested cancellation due to change of event schedule. Full refund processed.",
                CreatedBy = "System",
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Id = Guid.Parse("91000000-0000-0000-0000-000000000016"),
                        ProductId = 15,
                        ProductName = "Emperador Light",
                        ProductUnit = "bottle",
                        UnitPrice = 165.442m,
                        Quantity = 4,
                        Discount = 0.00m,
                        TotalPrice = 661.77m
                    },
                    new OrderItem
                    {
                        Id = Guid.Parse("91000000-0000-0000-0000-000000000017"),
                        ProductId = 13,
                        ProductName = "Tanduay White Rum",
                        ProductUnit = "bottle",
                        UnitPrice = 125.375m,
                        Quantity = 4,
                        Discount = 0.00m,
                        TotalPrice = 501.50m
                    }
                }
            }
        };

        db.Orders.AddRange(orders);
        db.SaveChanges();
    }
}
