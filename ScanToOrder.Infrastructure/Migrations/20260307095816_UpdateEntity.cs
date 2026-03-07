using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_AddOns_AddOnId",
                table: "Subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_Tenants_TenantId",
                table: "Subscriptions");

            migrationBuilder.DropTable(
                name: "AddOns");

            migrationBuilder.DropTable(
                name: "CashDrawerReports");

            migrationBuilder.DropTable(
                name: "PointHistory");

            migrationBuilder.DropTable(
                name: "WalletTransactions");

            migrationBuilder.DropTable(
                name: "MemberPoints");

            migrationBuilder.DropTable(
                name: "AdminWallet");

            migrationBuilder.DropTable(
                name: "TenantWallets");

            migrationBuilder.DropColumn(
                name: "TotalCategories",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TotalDishes",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TotalRestaurants",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "Plans");

            migrationBuilder.RenameColumn(
                name: "AddOnId",
                table: "Subscriptions",
                newName: "RestaurantId");

            migrationBuilder.RenameIndex(
                name: "IX_Subscriptions_AddOnId",
                table: "Subscriptions",
                newName: "IX_Subscriptions_RestaurantId");

            migrationBuilder.RenameColumn(
                name: "MaxRestaurantsCount",
                table: "Plans",
                newName: "YearlyPrice");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Plans",
                newName: "Status");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Subscriptions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Subscriptions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Staffs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Staffs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Staffs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAvailableShift",
                table: "Restaurants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PresentCashierId",
                table: "Restaurants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DailyRateMonth",
                table: "Plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DailyRateYear",
                table: "Plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyPrice",
                table: "Plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TransactionCode = table.Column<string>(type: "text", nullable: false),
                    TotalAmount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantId = table.Column<int>(type: "integer", nullable: false),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OpeningCashAmount = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shifts_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Shifts_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantId = table.Column<int>(type: "integer", nullable: false),
                    PaymentTransactionId = table.Column<int>(type: "integer", nullable: false),
                    OldPlanId = table.Column<int>(type: "integer", nullable: false),
                    NewPlanId = table.Column<int>(type: "integer", nullable: false),
                    ActionType = table.Column<string>(type: "text", nullable: false),
                    AmountAllocated = table.Column<decimal>(type: "numeric", nullable: false),
                    BalanceConvereted = table.Column<decimal>(type: "numeric", nullable: false),
                    DaysAdded = table.Column<int>(type: "integer", nullable: false),
                    OldExpired = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NewExpired = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionLogs_PaymentTransactions_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubscriptionLogs_Plans_NewPlanId",
                        column: x => x.NewPlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionLogs_Plans_OldPlanId",
                        column: x => x.OldPlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionLogs_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShiftReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShiftId = table.Column<int>(type: "integer", nullable: false),
                    ReportDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalCashOrder = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalTransferOrder = table.Column<decimal>(type: "numeric", nullable: false),
                    ActualCashAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Difference = table.Column<decimal>(type: "numeric", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftReports_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_TenantId",
                table: "PaymentTransactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftReports_ShiftId",
                table: "ShiftReports",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_RestaurantId",
                table: "Shifts",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_StaffId",
                table: "Shifts",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionLogs_NewPlanId",
                table: "SubscriptionLogs",
                column: "NewPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionLogs_OldPlanId",
                table: "SubscriptionLogs",
                column: "OldPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionLogs_PaymentTransactionId",
                table: "SubscriptionLogs",
                column: "PaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionLogs_RestaurantId",
                table: "SubscriptionLogs",
                column: "RestaurantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Restaurants_RestaurantId",
                table: "Subscriptions",
                column: "RestaurantId",
                principalTable: "Restaurants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Tenants_TenantId",
                table: "Subscriptions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_Restaurants_RestaurantId",
                table: "Subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_Tenants_TenantId",
                table: "Subscriptions");

            migrationBuilder.DropTable(
                name: "ShiftReports");

            migrationBuilder.DropTable(
                name: "SubscriptionLogs");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Staffs");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Staffs");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Staffs");

            migrationBuilder.DropColumn(
                name: "IsAvailableShift",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "PresentCashierId",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "DailyRateMonth",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "DailyRateYear",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "MonthlyPrice",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Orders");

            migrationBuilder.RenameColumn(
                name: "RestaurantId",
                table: "Subscriptions",
                newName: "AddOnId");

            migrationBuilder.RenameIndex(
                name: "IX_Subscriptions_RestaurantId",
                table: "Subscriptions",
                newName: "IX_Subscriptions_AddOnId");

            migrationBuilder.RenameColumn(
                name: "YearlyPrice",
                table: "Plans",
                newName: "MaxRestaurantsCount");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Plans",
                newName: "Description");

            migrationBuilder.AddColumn<int>(
                name: "TotalCategories",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalDishes",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalRestaurants",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Subscriptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Subscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Plans",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "AddOns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    MaxCategoriesCount = table.Column<int>(type: "integer", nullable: false),
                    MaxDishesCount = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddOns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminWallet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommissionBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminWallet", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashDrawerReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantId = table.Column<int>(type: "integer", nullable: false),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualCashAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Difference = table.Column<decimal>(type: "numeric", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ReportDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalCashOrder = table.Column<decimal>(type: "numeric", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashDrawerReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashDrawerReports_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CashDrawerReports_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberPoints",
                columns: table => new
                {
                    MemberPointId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentPoint = table.Column<int>(type: "integer", nullable: false),
                    RedeemAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberPoints", x => x.MemberPointId);
                    table.ForeignKey(
                        name: "FK_MemberPoints_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantWallets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsBlocked = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WalletBalance = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantWallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantWallets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PointHistory",
                columns: table => new
                {
                    PointHistoryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MemberPointId = table.Column<int>(type: "integer", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Point = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointHistory", x => x.PointHistoryId);
                    table.ForeignKey(
                        name: "FK_PointHistory_MemberPoints_MemberPointId",
                        column: x => x.MemberPointId,
                        principalTable: "MemberPoints",
                        principalColumn: "MemberPointId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WalletTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdminWalletId = table.Column<int>(type: "integer", nullable: true),
                    SubscriptionId = table.Column<int>(type: "integer", nullable: true),
                    TenantWalletId = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric", nullable: false),
                    BalanceBefore = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<int>(type: "integer", nullable: true),
                    OrderCode = table.Column<long>(type: "bigint", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TransactionStatus = table.Column<int>(type: "integer", nullable: false),
                    TransactionType = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WalletType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTransactions", x => x.Id);
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
                name: "IX_CashDrawerReports_RestaurantId",
                table: "CashDrawerReports",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerReports_StaffId",
                table: "CashDrawerReports",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberPoints_CustomerId",
                table: "MemberPoints",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PointHistory_MemberPointId",
                table: "PointHistory",
                column: "MemberPointId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantWallets_TenantId",
                table: "TenantWallets",
                column: "TenantId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_AddOns_AddOnId",
                table: "Subscriptions",
                column: "AddOnId",
                principalTable: "AddOns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Tenants_TenantId",
                table: "Subscriptions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
