using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundClawbackProcessedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClawbackProcessedAt",
                table: "TRANSACTION",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRANSACTION_ClawbackProcessedAt",
                table: "TRANSACTION",
                column: "ClawbackProcessedAt",
                filter: "[ClawbackProcessedAt] IS NULL AND [OriginalTransactionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TRANSACTION_ClawbackProcessedAt",
                table: "TRANSACTION");

            migrationBuilder.DropColumn(
                name: "ClawbackProcessedAt",
                table: "TRANSACTION");
        }
    }
}
