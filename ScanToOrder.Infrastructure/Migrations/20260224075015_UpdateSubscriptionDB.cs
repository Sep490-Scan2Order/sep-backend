using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSubscriptionDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_AddOns_AddOnId",
                table: "Subscriptions");

            migrationBuilder.AlterColumn<int>(
                name: "AddOnId",
                table: "Subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_AddOns_AddOnId",
                table: "Subscriptions",
                column: "AddOnId",
                principalTable: "AddOns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_AddOns_AddOnId",
                table: "Subscriptions");

            migrationBuilder.AlterColumn<int>(
                name: "AddOnId",
                table: "Subscriptions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_AddOns_AddOnId",
                table: "Subscriptions",
                column: "AddOnId",
                principalTable: "AddOns",
                principalColumn: "Id");
        }
    }
}
