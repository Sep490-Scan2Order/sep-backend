using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDefaultToMenuTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "MenuTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE \"MenuTemplates\" SET \"IsDefault\" = true WHERE \"TemplateName\" = 'Classic Template';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "MenuTemplates");
        }
    }
}
