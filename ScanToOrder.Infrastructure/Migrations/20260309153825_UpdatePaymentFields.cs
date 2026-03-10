using System;
using Microsoft.EntityFrameworkCore.Migrations;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePaymentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop TenantId safely — IF EXISTS to handle partial migration runs or manual drops
            migrationBuilder.Sql("""
                ALTER TABLE "Subscriptions" DROP CONSTRAINT IF EXISTS "FK_Subscriptions_Tenants_TenantId";
                DROP INDEX IF EXISTS "IX_Subscriptions_TenantId";
                ALTER TABLE "Subscriptions" DROP COLUMN IF EXISTS "TenantId";
                """);

            // Add Level column safely
            migrationBuilder.Sql("""
                ALTER TABLE "Plans" ADD COLUMN IF NOT EXISTS "Level" integer NOT NULL DEFAULT 0;
                """);

            // Add Payload column to PaymentTransactions safely with default empty JSON
            migrationBuilder.Sql("""
                ALTER TABLE "PaymentTransactions" ADD COLUMN IF NOT EXISTS "Payload" jsonb NOT NULL DEFAULT '{}';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Level",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "Payload",
                table: "PaymentTransactions");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId",
                table: "Subscriptions",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Tenants_TenantId",
                table: "Subscriptions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");
        }
    }
}
