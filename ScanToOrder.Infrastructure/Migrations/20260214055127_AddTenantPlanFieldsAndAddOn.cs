using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPlanFieldsAndAddOn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<int>(
                name: "AddOnId",
                table: "Subscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxRestaurantsCount",
                table: "Plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AddOns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MaxDishesCount = table.Column<int>(type: "integer", nullable: false),
                    MaxCategoriesCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddOns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_AddOnId",
                table: "Subscriptions",
                column: "AddOnId");

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_AddOns_AddOnId",
                table: "Subscriptions",
                column: "AddOnId",
                principalTable: "AddOns",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_AddOns_AddOnId",
                table: "Subscriptions");

            migrationBuilder.DropTable(
                name: "AddOns");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_AddOnId",
                table: "Subscriptions");

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
                name: "AddOnId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "MaxRestaurantsCount",
                table: "Plans");
        }
    }
}
