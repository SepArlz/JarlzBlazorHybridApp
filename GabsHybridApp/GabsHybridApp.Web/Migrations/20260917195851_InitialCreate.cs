using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GabsHybridApp.Web.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "GabsHybridApp");

            migrationBuilder.CreateTable(
                name: "BackupRecords",
                schema: "GabsHybridApp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TotalRecordsCount = table.Column<int>(type: "integer", nullable: false),
                    TableBreakdownJson = table.Column<string>(type: "text", nullable: false),
                    BackupType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AppVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceFormFactor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourcePlatform = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "GabsHybridApp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CustomerEmail = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ShippingAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BillingAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PaymentStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrderDateUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ShippedDateUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeliveredDateUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SubTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ShippingFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "GabsHybridApp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Unit = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PictureFilename = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegisteredDevices",
                schema: "GabsHybridApp",
                columns: table => new
                {
                    DeviceId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AssignedLocation = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RegisteredOn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastSyncUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisteredDevices", x => x.DeviceId);
                });

            migrationBuilder.CreateTable(
                name: "UserAccounts",
                schema: "GabsHybridApp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    PasswordSalt = table.Column<byte[]>(type: "bytea", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Roles = table.Column<string>(type: "text", nullable: true),
                    ServerSalt = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                schema: "GabsHybridApp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: true),
                    ProductName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProductUnit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Discount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "GabsHybridApp",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "GabsHybridApp",
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "GabsHybridApp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    NotificationType = table.Column<int>(type: "integer", nullable: false),
                    NavigateToUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_UserAccounts_UserId",
                        column: x => x.UserId,
                        principalSchema: "GabsHybridApp",
                        principalTable: "UserAccounts",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                schema: "GabsHybridApp",
                table: "Products",
                columns: new[] { "Id", "Description", "Name", "PictureFilename", "Unit", "UnitPrice" },
                values: new object[,]
                {
                    { 1, "Original scent, 64 loads", "Tide Laundry Detergent", null, "bottle", 350.00m },
                    { 2, "Size 4, 120 count", "Pampers Diapers", null, "box", 999.00m },
                    { 3, "With 1 blade cartridge", "Gillette Fusion Razor", null, "pack", 199.75m },
                    { 4, "4 bars, moisturizing cream", "Dove Beauty Bar", null, "pack", 75.00m },
                    { 5, "Real, 30 oz", "Hellmann's Mayonnaise", null, "jar", 120.00m },
                    { 6, "Phoenix scent, 4 oz", "Axe Body Spray", null, "bottle", 190.00m },
                    { 7, "Classic, 12 oz", "Nescafe Instant Coffee", null, "jar", 170.00m },
                    { 8, "Milk chocolate, 1.5 oz", "KitKat Chocolate Bar", null, "bar", 30.00m },
                    { 9, "24-pack, 16.9 oz bottles", "Nestle Pure Life Water", null, "pack", 180.00m },
                    { 10, "1 Liter bottle of Coca-Cola", "Coca-Cola 1 Liter", null, "bottle", 30.00m },
                    { 11, "12 fl oz can of Sprite", "Sprite 12 oz", null, "can", 12.00m },
                    { 12, "Orange juice with pulp, 16.9 fl oz bottle", "Minute Maid Pulpy Orange", null, "bottle", 25.00m },
                    { 13, "750ml", "Tanduay White Rum", null, "bottle", 125.375m },
                    { 14, "1000ml", "Red Horse Beer", null, "bottle", 118.0625m },
                    { 15, "1L blended premium brandy", "Emperador Light", null, "bottle", 165.442m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                schema: "GabsHybridApp",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                schema: "GabsHybridApp",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                schema: "GabsHybridApp",
                table: "OrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNumber",
                schema: "GabsHybridApp",
                table: "Orders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegisteredDevices_DeviceId",
                schema: "GabsHybridApp",
                table: "RegisteredDevices",
                column: "DeviceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupRecords",
                schema: "GabsHybridApp");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "GabsHybridApp");

            migrationBuilder.DropTable(
                name: "OrderItems",
                schema: "GabsHybridApp");

            migrationBuilder.DropTable(
                name: "RegisteredDevices",
                schema: "GabsHybridApp");

            migrationBuilder.DropTable(
                name: "UserAccounts",
                schema: "GabsHybridApp");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "GabsHybridApp");

            migrationBuilder.DropTable(
                name: "Products",
                schema: "GabsHybridApp");
        }
    }
}
