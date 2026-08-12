using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CampaignSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "MERCHANT",
                columns: new[] { "Id", "IsActive", "MerchantName", "MerchantNumber" },
                values: new object[,]
                {
                    { 1, true, "Grande Cafe", "000145" },
                    { 2, true, "Köfteci Yusuf", "000912" },
                    { 3, true, "Opet", "000874" }
                });

            migrationBuilder.InsertData(
                table: "PRODUCT",
                columns: new[] { "Id", "ProductCode", "ProductName" },
                values: new object[,]
                {
                    { 1, "201", "Visa Classic" },
                    { 2, "202", "MasterCard Classic" },
                    { 3, "203", "Visa Gold" },
                    { 4, "204", "MasterCard Gold" },
                    { 5, "205", "Platinum Plus" },
                    { 6, "206", "Platinum Plus Metal" }
                });

            migrationBuilder.InsertData(
                table: "SEGMENT",
                columns: new[] { "Id", "SegmentCode", "SegmentName" },
                values: new object[,]
                {
                    { 1, "OGR", "Student" },
                    { 2, "PER", "Company Employee" },
                    { 3, "CFT", "Farmer" },
                    { 4, "EVH", "Homemaker" },
                    { 5, "EMK", "Retiree" }
                });

            migrationBuilder.InsertData(
                table: "TRANSACTION_CODE",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[,]
                {
                    { 1, "SA", "Sale" },
                    { 2, "NA", "Cash Advance" },
                    { 3, "OD", "Debt Payment" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "PRODUCT",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "PRODUCT",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "PRODUCT",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "PRODUCT",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "PRODUCT",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "PRODUCT",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "SEGMENT",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "SEGMENT",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "SEGMENT",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "SEGMENT",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "SEGMENT",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "TRANSACTION_CODE",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "TRANSACTION_CODE",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "TRANSACTION_CODE",
                keyColumn: "Id",
                keyValue: 3);
        }
    }
}
