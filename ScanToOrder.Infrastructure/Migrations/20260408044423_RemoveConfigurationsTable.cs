using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveConfigurationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TABLE IF EXISTS "Configurations";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Configurations",
                columns: table => new
                {
                    CommissionRate = table.Column<int>(type: "integer", nullable: false),
                    ExpiredDuration = table.Column<int>(type: "integer", nullable: false),
                    LastUpdated = table.Column<DateOnly>(type: "date", nullable: false),
                    RedeemRate = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                });
        }
    }
}
