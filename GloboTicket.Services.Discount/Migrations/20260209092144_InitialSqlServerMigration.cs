using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GloboTicket.Services.Discount.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServerMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Coupons",
                columns: table => new
                {
                    CouponId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coupons", x => x.CouponId);
                });

            migrationBuilder.InsertData(
                table: "Coupons",
                columns: new[] { "CouponId", "Amount", "UserId" },
                values: new object[,]
                {
                    { new Guid("53447bb6-8545-4799-95dd-fdf75d2afa50"), 10, new Guid("e455a3df-7fa5-47e0-8435-179b300d531f") },
                    { new Guid("53447bb6-8545-4799-95dd-fdf75d2afa51"), 20, new Guid("bbf594b0-3761-4a65-b04c-eec4836d9070") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Coupons");
        }
    }
}
