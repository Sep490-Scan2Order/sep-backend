using System;
using Microsoft.EntityFrameworkCore.Migrations;
using ScanToOrder.Domain.Enums;

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

            migrationBuilder.AddColumn<PlanFeaturesConfig>(
                name: "Features",
                table: "Plans",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{\"MaxStaff\":2,\"CanUseCombo\":false,\"CanUsePromotions\":false,\"CanCustomMenuTemplate\":false}'::jsonb");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_RestaurantId",
                table: "Subscriptions",
                column: "RestaurantId",
                unique: true);
        }

    }
}
