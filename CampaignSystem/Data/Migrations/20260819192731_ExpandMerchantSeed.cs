using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CampaignSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExpandMerchantSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 1,
                column: "MerchantNumber",
                value: "300145782");

            migrationBuilder.UpdateData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 2,
                column: "MerchantNumber",
                value: "300912467");

            migrationBuilder.UpdateData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 3,
                column: "MerchantNumber",
                value: "410874193");

            migrationBuilder.InsertData(
                table: "MERCHANT",
                columns: new[] { "Id", "IsActive", "MerchantName", "MerchantNumber" },
                values: new object[,]
                {
                    { 4, true, "Shell", "410336729" },
                    { 5, true, "Petrol Ofisi", "410771056" },
                    { 6, true, "Migros", "520419863" },
                    { 7, true, "BİM", "520684137" },
                    { 8, true, "A101", "520297540" },
                    { 9, true, "Teknosa", "610853024" },
                    { 10, true, "Vatan Bilgisayar", "610140678" },
                    { 11, true, "LC Waikiki", "710962385" },
                    { 12, true, "Big Chefs", "300558214" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.UpdateData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 1,
                column: "MerchantNumber",
                value: "000145");

            migrationBuilder.UpdateData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 2,
                column: "MerchantNumber",
                value: "000912");

            migrationBuilder.UpdateData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 3,
                column: "MerchantNumber",
                value: "000874");
        }
    }
}
