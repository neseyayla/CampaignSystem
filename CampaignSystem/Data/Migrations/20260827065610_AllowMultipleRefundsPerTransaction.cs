using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleRefundsPerTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TRANSACTION_OriginalTransactionId",
                table: "TRANSACTION");

            migrationBuilder.CreateIndex(
                name: "IX_TRANSACTION_OriginalTransactionId",
                table: "TRANSACTION",
                column: "OriginalTransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TRANSACTION_OriginalTransactionId",
                table: "TRANSACTION");

            migrationBuilder.CreateIndex(
                name: "IX_TRANSACTION_OriginalTransactionId",
                table: "TRANSACTION",
                column: "OriginalTransactionId",
                unique: true,
                filter: "[OriginalTransactionId] IS NOT NULL");
        }
    }
}
