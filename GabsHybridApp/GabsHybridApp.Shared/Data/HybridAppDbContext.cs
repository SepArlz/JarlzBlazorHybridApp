using GabsHybridApp.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace GabsHybridApp.Shared.Data;

public class HybridAppDbContext : DbContext
{
    public HybridAppDbContext(DbContextOptions<HybridAppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var schema = this.GetService<IConfiguration>().GetConnectionString("Schema");
        if (!string.IsNullOrWhiteSpace(schema) && !Database.IsSqlite())
        {
            modelBuilder.HasDefaultSchema(schema);
        }

        // Product config
        modelBuilder.Entity<Product>(e =>
        {
            // Seed data (uses your static SeedData() list)
            e.HasData(Product.SeedData());
        });

        // Order config
        modelBuilder.Entity<Order>(e =>
        {
            e.HasIndex(o => o.OrderNumber).IsUnique();
            e.HasMany(o => o.Items)
             .WithOne(i => i.Order)
             .HasForeignKey(i => i.OrderId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // RegisteredDevice config
        modelBuilder.Entity<RegisteredDevice>(e =>
        {
            e.HasIndex(d => d.DeviceId).IsUnique();
        });
    }

    public DbSet<UserAccount> UserAccounts { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<RegisteredDevice> RegisteredDevices { get; set; }
    public DbSet<BackupRecord> BackupRecords { get; set; }
}