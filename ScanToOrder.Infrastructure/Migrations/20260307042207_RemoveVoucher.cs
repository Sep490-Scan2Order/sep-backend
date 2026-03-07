using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ScanToOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveVoucher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop FKs với IF EXISTS (tên constraint trên DB có thể khác)
            migrationBuilder.Sql(@"
                ALTER TABLE ""Orders"" DROP CONSTRAINT IF EXISTS ""FK_Orders_MemberVouchers_MemberVoucherId"";
                ALTER TABLE ""PointHistory"" DROP CONSTRAINT IF EXISTS ""FK_PointHistory_MemberVouchers_MemberVoucherId"";
            ");

            // Drop tables với CASCADE để xóa luôn các FK phụ thuộc (nếu tên constraint khác)
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS ""MemberVouchers"" CASCADE;
                DROP TABLE IF EXISTS ""Vouchers"" CASCADE;
            ");

            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_PointHistory_MemberVoucherId"";
                DROP INDEX IF EXISTS ""IX_Orders_MemberVoucherId"";
            ");

            migrationBuilder.DropColumn(
                name: "MemberVoucherId",
                table: "PointHistory");

            migrationBuilder.DropColumn(
                name: "MemberVoucherId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VoucherDiscount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VoucherRate",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "VoucherBalance",
                table: "AdminWallet");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MemberVoucherId",
                table: "PointHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MemberVoucherId",
                table: "Orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VoucherDiscount",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "VoucherRate",
                table: "Configurations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "VoucherBalance",
                table: "AdminWallet",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "numeric", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    MinOrderAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PointRequire = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemberVouchers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VoucherId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberVouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberVouchers_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PointHistory_MemberVoucherId",
                table: "PointHistory",
                column: "MemberVoucherId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_MemberVoucherId",
                table: "Orders",
                column: "MemberVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberVouchers_VoucherId",
                table: "MemberVouchers",
                column: "VoucherId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_MemberVouchers_MemberVoucherId",
                table: "Orders",
                column: "MemberVoucherId",
                principalTable: "MemberVouchers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PointHistory_MemberVouchers_MemberVoucherId",
                table: "PointHistory",
                column: "MemberVoucherId",
                principalTable: "MemberVouchers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
