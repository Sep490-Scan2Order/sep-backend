using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    public partial class UpdateTenantStatusToBool : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Tenants""
                ALTER COLUMN ""Status"" TYPE boolean
                USING CASE
                    WHEN ""Status"" = 'Active' THEN true
                    ELSE false
                END;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Tenants""
                ALTER COLUMN ""Status"" TYPE text
                USING CASE
                    WHEN ""Status"" = true THEN 'Active'
                    ELSE 'Blocked'
                END;
            ");
        }
    }
}