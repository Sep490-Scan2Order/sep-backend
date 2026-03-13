using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftIdToTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ShiftId",
                table: "Transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ShiftId",
                table: "Transactions",
                column: "ShiftId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Shifts_ShiftId",
                table: "Transactions",
                column: "ShiftId",
                principalTable: "Shifts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Shifts_ShiftId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ShiftId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "Transactions");
        }
    }
}
