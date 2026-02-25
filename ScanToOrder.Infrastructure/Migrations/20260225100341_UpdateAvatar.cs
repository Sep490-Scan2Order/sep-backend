using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAvatar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Avatar",
                table: "Staffs");

            migrationBuilder.AddColumn<string>(
                name: "Avatar",
                table: "AuthenticationUsers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Avatar",
                table: "AuthenticationUsers");

            migrationBuilder.AddColumn<string>(
                name: "Avatar",
                table: "Staffs",
                type: "text",
                nullable: true);
        }
    }
}
