using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveQrCodeAddUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PickupToken",
                table: "Orders");

            migrationBuilder.AddColumn<string>(
                name: "QrCodeUrl",
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QrCodeUrl",
                table: "Orders");

            migrationBuilder.AddColumn<Guid>(
                name: "PickupToken",
                table: "Orders",
                type: "uuid",
                nullable: true);
        }
    }
}
