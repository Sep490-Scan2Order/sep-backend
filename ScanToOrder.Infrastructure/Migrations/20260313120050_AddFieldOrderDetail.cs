using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldOrderDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Price",
                table: "OrderDetails",
                newName: "PromotionAmount");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountedPrice",
                table: "OrderDetails",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalPrice",
                table: "OrderDetails",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountedPrice",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "OriginalPrice",
                table: "OrderDetails");

            migrationBuilder.RenameColumn(
                name: "PromotionAmount",
                table: "OrderDetails",
                newName: "Price");
        }
    }
}
