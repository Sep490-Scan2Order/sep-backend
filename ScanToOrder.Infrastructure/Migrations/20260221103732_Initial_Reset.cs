using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial_Reset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AlterDatabase()
        .Annotation("Npgsql:PostgresExtension:postgis", ",,");

    migrationBuilder.Sql("DROP TABLE IF EXISTS public.\"WalletTransactions\" CASCADE;");

    migrationBuilder.CreateTable(
        name: "WalletTransactions",
        columns: table => new
        {
            Id = table.Column<int>(type: "integer", nullable: false)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            TenantWalletId = table.Column<int>(type: "integer", nullable: true),
            AdminWalletId = table.Column<int>(type: "integer", nullable: true),
            SubscriptionId = table.Column<int>(type: "integer", nullable: true),
            Amount = table.Column<decimal>(type: "numeric", nullable: false),
            BalanceBefore = table.Column<decimal>(type: "numeric", nullable: false),
            BalanceAfter = table.Column<decimal>(type: "numeric", nullable: false),
            OrderCode = table.Column<long>(type: "bigint", nullable: false),
            PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            TransactionStatus = table.Column<int>(type: "integer", nullable: false),
            WalletType = table.Column<int>(type: "integer", nullable: false),
            TransactionType = table.Column<int>(type: "integer", nullable: false),
            Note = table.Column<int>(type: "integer", nullable: true),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_WalletTransactions", x => x.Id);
            // Các FK này nối tới bảng đã tồn tại trong DB của bạn
            table.ForeignKey(
                name: "FK_WalletTransactions_AdminWallet_AdminWalletId",
                column: x => x.AdminWalletId,
                principalTable: "AdminWallet",
                principalColumn: "Id");
            table.ForeignKey(
                name: "FK_WalletTransactions_Subscriptions_SubscriptionId",
                column: x => x.SubscriptionId,
                principalTable: "Subscriptions",
                principalColumn: "Id");
            table.ForeignKey(
                name: "FK_WalletTransactions_TenantWallets_TenantWalletId",
                column: x => x.TenantWalletId,
                principalTable: "TenantWallets",
                principalColumn: "Id");
        });

    migrationBuilder.CreateIndex(
        name: "IX_WalletTransactions_AdminWalletId",
        table: "WalletTransactions",
        column: "AdminWalletId");

    migrationBuilder.CreateIndex(
        name: "IX_WalletTransactions_SubscriptionId",
        table: "WalletTransactions",
        column: "SubscriptionId");

    migrationBuilder.CreateIndex(
        name: "IX_WalletTransactions_TenantWalletId",
        table: "WalletTransactions",
        column: "TenantWalletId");
}

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BranchDishConfig");

            migrationBuilder.DropTable(
                name: "CashDrawerReports");

            migrationBuilder.DropTable(
                name: "Configurations");

            migrationBuilder.DropTable(
                name: "MenuRestaurants");

            migrationBuilder.DropTable(
                name: "NotifyTenant");

            migrationBuilder.DropTable(
                name: "OrderDetails");

            migrationBuilder.DropTable(
                name: "PointHistory");

            migrationBuilder.DropTable(
                name: "RestaurantPromotions");

            migrationBuilder.DropTable(
                name: "SystemBlog");

            migrationBuilder.DropTable(
                name: "WalletTransactions");

            migrationBuilder.DropTable(
                name: "Staffs");

            migrationBuilder.DropTable(
                name: "MenuTemplates");

            migrationBuilder.DropTable(
                name: "Notification");

            migrationBuilder.DropTable(
                name: "Dishes");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "MemberPoints");

            migrationBuilder.DropTable(
                name: "AdminWallet");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "TenantWallets");

            migrationBuilder.DropTable(
                name: "Category");

            migrationBuilder.DropTable(
                name: "MemberVouchers");

            migrationBuilder.DropTable(
                name: "Promotions");

            migrationBuilder.DropTable(
                name: "Restaurants");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "AddOns");

            migrationBuilder.DropTable(
                name: "Plans");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropTable(
                name: "AuthenticationUsers");
        }
    }
}
