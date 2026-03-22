using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.AlterColumn<Vector>(
                name: "SearchVector",
                table: "Restaurants",
                type: "vector(1536)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector",
                oldNullable: true);

            migrationBuilder.AlterColumn<Vector>(
                name: "SearchVector",
                table: "Dishes",
                type: "vector(1536)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_Description",
                table: "Restaurants",
                column: "Description")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_RestaurantName",
                table: "Restaurants",
                column: "RestaurantName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_SearchVector",
                table: "Restaurants",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Dishes_Description",
                table: "Dishes",
                column: "Description")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Dishes_DishName",
                table: "Dishes",
                column: "DishName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Dishes_SearchVector",
                table: "Dishes",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Restaurants_Description",
                table: "Restaurants");

            migrationBuilder.DropIndex(
                name: "IX_Restaurants_RestaurantName",
                table: "Restaurants");

            migrationBuilder.DropIndex(
                name: "IX_Restaurants_SearchVector",
                table: "Restaurants");

            migrationBuilder.DropIndex(
                name: "IX_Dishes_Description",
                table: "Dishes");

            migrationBuilder.DropIndex(
                name: "IX_Dishes_DishName",
                table: "Dishes");

            migrationBuilder.DropIndex(
                name: "IX_Dishes_SearchVector",
                table: "Dishes");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AlterColumn<Vector>(
                name: "SearchVector",
                table: "Restaurants",
                type: "vector",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(1536)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Vector>(
                name: "SearchVector",
                table: "Dishes",
                type: "vector",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(1536)",
                oldNullable: true);
        }
    }
}
