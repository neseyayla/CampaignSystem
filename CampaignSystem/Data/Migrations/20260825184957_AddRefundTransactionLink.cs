using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundTransactionLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OriginalTransactionId",
                table: "TRANSACTION",
                type: "bigint",
                nullable: true);

            migrationBuilder.InsertData(
                table: "TRANSACTION_CODE",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[] { 4, "IA", "İade" });

            migrationBuilder.CreateIndex(
                name: "IX_TRANSACTION_OriginalTransactionId",
                table: "TRANSACTION",
                column: "OriginalTransactionId",
                unique: true,
                filter: "[OriginalTransactionId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_TRANSACTION_TRANSACTION_OriginalTransactionId",
                table: "TRANSACTION",
                column: "OriginalTransactionId",
                principalTable: "TRANSACTION",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TRANSACTION_TRANSACTION_OriginalTransactionId",
                table: "TRANSACTION");

            migrationBuilder.DropIndex(
                name: "IX_TRANSACTION_OriginalTransactionId",
                table: "TRANSACTION");

            migrationBuilder.DeleteData(
                table: "TRANSACTION_CODE",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DropColumn(
                name: "OriginalTransactionId",
                table: "TRANSACTION");
        }
    }
}
