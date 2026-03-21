using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHandOverReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HandoverCashAmount",
                table: "ShiftReports");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "HandoverCashAmount",
                table: "ShiftReports",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
