using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBranchDishConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BranchDishConfig_Dishes_DishId",
                table: "BranchDishConfig");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchDishConfig_Restaurants_RestaurantId",
                table: "BranchDishConfig");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BranchDishConfig",
                table: "BranchDishConfig");

            migrationBuilder.RenameTable(
                name: "BranchDishConfig",
                newName: "BranchDishConfigs");

            migrationBuilder.RenameIndex(
                name: "IX_BranchDishConfig_RestaurantId",
                table: "BranchDishConfigs",
                newName: "IX_BranchDishConfigs_RestaurantId");

            migrationBuilder.RenameIndex(
                name: "IX_BranchDishConfig_DishId",
                table: "BranchDishConfigs",
                newName: "IX_BranchDishConfigs_DishId");

            migrationBuilder.AlterColumn<string>(
                name: "CategoryName",
                table: "Categories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BranchDishConfigs",
                table: "BranchDishConfigs",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BranchDishConfigs_Dishes_DishId",
                table: "BranchDishConfigs",
                column: "DishId",
                principalTable: "Dishes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchDishConfigs_Restaurants_RestaurantId",
                table: "BranchDishConfigs",
                column: "RestaurantId",
                principalTable: "Restaurants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BranchDishConfigs_Dishes_DishId",
                table: "BranchDishConfigs");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchDishConfigs_Restaurants_RestaurantId",
                table: "BranchDishConfigs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BranchDishConfigs",
                table: "BranchDishConfigs");

            migrationBuilder.RenameTable(
                name: "BranchDishConfigs",
                newName: "BranchDishConfig");

            migrationBuilder.RenameIndex(
                name: "IX_BranchDishConfigs_RestaurantId",
                table: "BranchDishConfig",
                newName: "IX_BranchDishConfig_RestaurantId");

            migrationBuilder.RenameIndex(
                name: "IX_BranchDishConfigs_DishId",
                table: "BranchDishConfig",
                newName: "IX_BranchDishConfig_DishId");

            migrationBuilder.AlterColumn<string>(
                name: "CategoryName",
                table: "Categories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddPrimaryKey(
                name: "PK_BranchDishConfig",
                table: "BranchDishConfig",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BranchDishConfig_Dishes_DishId",
                table: "BranchDishConfig",
                column: "DishId",
                principalTable: "Dishes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchDishConfig_Restaurants_RestaurantId",
                table: "BranchDishConfig",
                column: "RestaurantId",
                principalTable: "Restaurants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
