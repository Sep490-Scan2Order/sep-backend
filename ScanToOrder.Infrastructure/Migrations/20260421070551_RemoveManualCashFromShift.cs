using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveManualCashFromShift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OpeningCashAmount",
                table: "Shifts");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ShiftTransfers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "ShiftTransfers");

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningCashAmount",
                table: "Shifts",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
