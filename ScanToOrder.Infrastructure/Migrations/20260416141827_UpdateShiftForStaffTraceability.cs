using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateShiftForStaffTraceability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentShiftId",
                table: "Shifts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Shifts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_ParentShiftId",
                table: "Shifts",
                column: "ParentShiftId");

            migrationBuilder.AddForeignKey(
                name: "FK_Shifts_Shifts_ParentShiftId",
                table: "Shifts",
                column: "ParentShiftId",
                principalTable: "Shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shifts_Shifts_ParentShiftId",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_ParentShiftId",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "ParentShiftId",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Shifts");
        }
    }
}
