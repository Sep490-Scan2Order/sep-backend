using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderIsScannedAndUpdateTenantFeeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextFeeDueAt",
                table: "Tenants");

            migrationBuilder.AddColumn<bool>(
                name: "IsScanned",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsScanned",
                table: "Orders");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextFeeDueAt",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
