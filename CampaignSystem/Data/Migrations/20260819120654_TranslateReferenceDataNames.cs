using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class TranslateReferenceDataNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "SEGMENT",
                keyColumn: "Id",
                keyValue: 1,
                column: "SegmentName",
                value: "Öğrenci");

            migrationBuilder.UpdateData(
                table: "SEGMENT",
                keyColumn: "Id",
                keyValue: 2,
                column: "SegmentName",
                value: "Şirket Çalışanı");

            migrationBuilder.UpdateData(
                table: "SEGMENT",
                keyColumn: "Id",
                keyValue: 3,
                column: "SegmentName",
                value: "Çiftçi");

            migrationBuilder.UpdateData(
                table: "SEGMENT",
                keyColumn: "Id",
                keyValue: 4,
                column: "SegmentName",
                value: "Ev Hanımı");

            migrationBuilder.UpdateData(
                table: "SEGMENT",
                keyColumn: "Id",
                keyValue: 5,
                column: "SegmentName",
                value: "Emekli");

            migrationBuilder.UpdateData(
                table: "TRANSACTION_CODE",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "Satış");

            migrationBuilder.UpdateData(
                table: "TRANSACTION_CODE",
                keyColumn: "Id",
                keyValue: 2,
                column: "Name",
                value: "Nakit Avans");

            migrationBuilder.UpdateData(
                table: "TRANSACTION_CODE",
                keyColumn: "Id",
                keyValue: 3,
                column: "Name",
                value: "Borç Ödeme");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "SEGMENT",
                keyColumn: "Id",
                keyValue: 1,
                column: "SegmentName",
                value: "Student");

            migrationBuilder.UpdateData(
                table: "SEGMENT",
                keyColumn: "Id",
                keyValue: 2,
                column: "SegmentName",
                value: "Company Employee");

            migrationBuilder.UpdateData(
                table: "SEGMENT",
                keyColumn: "Id",
                keyValue: 3,
                column: "SegmentName",
                value: "Farmer");

            migrationBuilder.UpdateData(
                table: "SEGMENT",
                keyColumn: "Id",
                keyValue: 4,
                column: "SegmentName",
                value: "Homemaker");

            migrationBuilder.UpdateData(
                table: "SEGMENT",
                keyColumn: "Id",
                keyValue: 5,
                column: "SegmentName",
                value: "Retiree");

            migrationBuilder.UpdateData(
                table: "TRANSACTION_CODE",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "Sale");

            migrationBuilder.UpdateData(
                table: "TRANSACTION_CODE",
                keyColumn: "Id",
                keyValue: 2,
                column: "Name",
                value: "Cash Advance");

            migrationBuilder.UpdateData(
                table: "TRANSACTION_CODE",
                keyColumn: "Id",
                keyValue: 3,
                column: "Name",
                value: "Debt Payment");
        }
    }
}
