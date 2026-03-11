using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLineVoidAndKitchenFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrinterId",
                table: "OrderLines");

            migrationBuilder.AddColumn<bool>(
                name: "IsSentToKitchen",
                table: "OrderLines",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVoided",
                table: "OrderLines",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentToKitchenAt",
                table: "OrderLines",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "OrderLines",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "OrderLines",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSentToKitchen",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "IsVoided",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "SentToKitchenAt",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "OrderLines");

            migrationBuilder.AddColumn<long>(
                name: "PrinterId",
                table: "OrderLines",
                type: "INTEGER",
                nullable: true);
        }
    }
}
