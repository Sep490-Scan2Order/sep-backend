using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantTotalOrderIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_TotalOrder",
                table: "Restaurants",
                column: "TotalOrder",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Restaurants_TotalOrder",
                table: "Restaurants");
        }
    }
}
