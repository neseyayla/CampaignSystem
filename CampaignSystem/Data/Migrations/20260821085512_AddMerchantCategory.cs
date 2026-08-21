using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CampaignSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MerchantCategoryId",
                table: "MERCHANT",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MERCHANT_CATEGORY",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    CategoryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MERCHANT_CATEGORY", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 1,
                column: "MerchantCategoryId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 2,
                column: "MerchantCategoryId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 3,
                column: "MerchantCategoryId",
                value: 3);

            migrationBuilder.UpdateData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 4,
                column: "MerchantCategoryId",
                value: 3);

            migrationBuilder.UpdateData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 5,
                column: "MerchantCategoryId",
                value: 3);

            migrationBuilder.UpdateData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 6,
                column: "MerchantCategoryId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 7,
                column: "MerchantCategoryId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 8,
                column: "MerchantCategoryId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 9,
                column: "MerchantCategoryId",
                value: 7);

            migrationBuilder.UpdateData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 10,
                column: "MerchantCategoryId",
                value: 7);

            migrationBuilder.UpdateData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 11,
                column: "MerchantCategoryId",
                value: 4);

            migrationBuilder.UpdateData(
                table: "MERCHANT",
                keyColumn: "Id",
                keyValue: 12,
                column: "MerchantCategoryId",
                value: 2);

            migrationBuilder.InsertData(
                table: "MERCHANT_CATEGORY",
                columns: new[] { "Id", "CategoryCode", "CategoryName" },
                values: new object[,]
                {
                    { 1, "GDA", "Gıda / Market" },
                    { 2, "RST", "Restoran / Yeme-İçme" },
                    { 3, "AKY", "Akaryakıt" },
                    { 4, "GYM", "Giyim" },
                    { 5, "AYK", "Ayakkabı & Aksesuar" },
                    { 6, "KOZ", "Kozmetik" },
                    { 7, "ELK", "Elektronik" },
                    { 8, "TEL", "Telekomünikasyon / GSM" },
                    { 9, "MOB", "Mobilya & Ev Tekstili" },
                    { 10, "BYZ", "Beyaz Eşya" },
                    { 11, "OTO", "Otomotiv & Oto Bakım" },
                    { 12, "ARK", "Araç Kiralama" },
                    { 13, "TUR", "Turizm / Seyahat / Otel" },
                    { 14, "HVY", "Havayolları / Ulaşım" },
                    { 15, "EGT", "Eğitim" },
                    { 16, "SGL", "Sağlık / Eczane / Optik" },
                    { 17, "SGR", "Sigorta" },
                    { 18, "SPR", "Spor" },
                    { 19, "KUY", "Kuyumculuk / Saat" },
                    { 20, "KRT", "Kırtasiye / Oyuncak" },
                    { 21, "YPI", "Yapı & İnşaat" },
                    { 22, "EGL", "Eğlence" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_MERCHANT_MerchantCategoryId",
                table: "MERCHANT",
                column: "MerchantCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MERCHANT_CATEGORY_CategoryCode",
                table: "MERCHANT_CATEGORY",
                column: "CategoryCode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MERCHANT_MERCHANT_CATEGORY_MerchantCategoryId",
                table: "MERCHANT",
                column: "MerchantCategoryId",
                principalTable: "MERCHANT_CATEGORY",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MERCHANT_MERCHANT_CATEGORY_MerchantCategoryId",
                table: "MERCHANT");

            migrationBuilder.DropTable(
                name: "MERCHANT_CATEGORY");

            migrationBuilder.DropIndex(
                name: "IX_MERCHANT_MerchantCategoryId",
                table: "MERCHANT");

            migrationBuilder.DropColumn(
                name: "MerchantCategoryId",
                table: "MERCHANT");
        }
    }
}
