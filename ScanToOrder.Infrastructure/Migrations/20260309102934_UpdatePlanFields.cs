using System;
using Microsoft.EntityFrameworkCore.Migrations;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePlanFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop TenantId safely — idempotent: works even if already dropped manually on DB
            migrationBuilder.Sql("""
                ALTER TABLE "Subscriptions" DROP CONSTRAINT IF EXISTS "FK_Subscriptions_Tenants_TenantId";
                DROP INDEX IF EXISTS "IX_Subscriptions_TenantId";
                ALTER TABLE "Subscriptions" DROP COLUMN IF EXISTS "TenantId";
                """);

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_RestaurantId",
                table: "Subscriptions");

            migrationBuilder.AlterColumn<Guid>(
                name: "PresentCashierId",
                table: "Restaurants",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            // Add column safely — idempotent: won't fail if column already exists from a partial migration run
            migrationBuilder.Sql("""
                ALTER TABLE "Plans" ADD COLUMN IF NOT EXISTS "Features" jsonb NOT NULL DEFAULT '{"MaxStaff":2,"CanUseCombo":false,"CanUsePromotions":false,"CanCustomMenuTemplate":false}';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_RestaurantId",
                table: "Subscriptions",
                column: "RestaurantId",
                unique: true);
        }

    }
}
