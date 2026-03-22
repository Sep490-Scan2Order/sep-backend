using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchVectorProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Vector>(
                name: "SearchVector",
                table: "Restaurants",
                type: "vector",
                nullable: true);

            migrationBuilder.AddColumn<Vector>(
                name: "SearchVector",
                table: "Dishes",
                type: "vector",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Dishes");
        }
    }
}
