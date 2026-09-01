using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CampaignSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSeasonalPattern : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SEASONAL_PATTERN",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MerchantCategoryId = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SEASONAL_PATTERN", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SEASONAL_PATTERN_MERCHANT_CATEGORY_MerchantCategoryId",
                        column: x => x.MerchantCategoryId,
                        principalTable: "MERCHANT_CATEGORY",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "SEASONAL_PATTERN",
                columns: new[] { "Id", "MerchantCategoryId", "Month", "Weight" },
                values: new object[,]
                {
                    { 1, 3, 1, 0.85m },
                    { 2, 3, 2, 0.85m },
                    { 3, 3, 6, 1.2m },
                    { 4, 3, 7, 1.35m },
                    { 5, 3, 8, 1.3m },
                    { 6, 3, 9, 1.05m },
                    { 7, 3, 12, 0.95m },
                    { 8, 4, 1, 1.1m },
                    { 9, 4, 2, 0.9m },
                    { 10, 4, 3, 1.2m },
                    { 11, 4, 4, 1.15m },
                    { 12, 4, 7, 0.85m },
                    { 13, 4, 9, 1.25m },
                    { 14, 4, 10, 1.15m },
                    { 15, 4, 11, 1.2m },
                    { 16, 4, 12, 1.15m },
                    { 17, 5, 3, 1.15m },
                    { 18, 5, 4, 1.1m },
                    { 19, 5, 7, 0.85m },
                    { 20, 5, 9, 1.25m },
                    { 21, 5, 10, 1.1m },
                    { 22, 5, 11, 1.15m },
                    { 23, 5, 12, 1.1m },
                    { 24, 7, 1, 0.8m },
                    { 25, 7, 2, 0.8m },
                    { 26, 7, 3, 0.9m },
                    { 27, 7, 8, 1.15m },
                    { 28, 7, 9, 1.2m },
                    { 29, 7, 11, 1.55m },
                    { 30, 7, 12, 1.25m },
                    { 31, 9, 1, 0.85m },
                    { 32, 9, 2, 0.85m },
                    { 33, 9, 5, 1.25m },
                    { 34, 9, 6, 1.3m },
                    { 35, 9, 7, 1.2m },
                    { 36, 9, 9, 1.1m },
                    { 37, 9, 11, 1.15m },
                    { 38, 10, 1, 0.8m },
                    { 39, 10, 2, 0.85m },
                    { 40, 10, 5, 1.25m },
                    { 41, 10, 6, 1.3m },
                    { 42, 10, 7, 1.15m },
                    { 43, 10, 11, 1.2m },
                    { 44, 10, 12, 1.15m },
                    { 45, 13, 1, 0.85m },
                    { 46, 13, 2, 1.15m },
                    { 47, 13, 3, 0.9m },
                    { 48, 13, 6, 1.35m },
                    { 49, 13, 7, 1.55m },
                    { 50, 13, 8, 1.5m },
                    { 51, 13, 9, 1.15m },
                    { 52, 13, 11, 0.8m },
                    { 53, 13, 12, 0.9m },
                    { 54, 14, 1, 0.9m },
                    { 55, 14, 2, 1.15m },
                    { 56, 14, 6, 1.25m },
                    { 57, 14, 7, 1.45m },
                    { 58, 14, 8, 1.4m },
                    { 59, 14, 11, 0.85m },
                    { 60, 14, 12, 1.1m },
                    { 61, 15, 1, 1.2m },
                    { 62, 15, 2, 1.25m },
                    { 63, 15, 4, 0.85m },
                    { 64, 15, 5, 0.85m },
                    { 65, 15, 6, 1.1m },
                    { 66, 15, 7, 1.1m },
                    { 67, 15, 8, 1.45m },
                    { 68, 15, 9, 1.6m },
                    { 69, 15, 10, 1.1m },
                    { 70, 15, 11, 0.9m },
                    { 71, 15, 12, 0.85m },
                    { 72, 18, 1, 1.45m },
                    { 73, 18, 2, 1.15m },
                    { 74, 18, 6, 0.85m },
                    { 75, 18, 7, 0.8m },
                    { 76, 18, 8, 0.85m },
                    { 77, 18, 9, 1.25m },
                    { 78, 18, 10, 1.1m },
                    { 79, 19, 2, 1.1m },
                    { 80, 19, 4, 1.15m },
                    { 81, 19, 5, 1.35m },
                    { 82, 19, 6, 1.3m },
                    { 83, 19, 7, 1.15m },
                    { 84, 19, 8, 0.85m },
                    { 85, 19, 9, 0.9m },
                    { 86, 19, 11, 1.2m },
                    { 87, 19, 12, 1.25m },
                    { 88, 20, 1, 1.25m },
                    { 89, 20, 2, 1.2m },
                    { 90, 20, 4, 1.1m },
                    { 91, 20, 5, 0.85m },
                    { 92, 20, 6, 0.8m },
                    { 93, 20, 7, 0.9m },
                    { 94, 20, 8, 1.55m },
                    { 95, 20, 9, 1.6m },
                    { 96, 20, 10, 0.9m },
                    { 97, 20, 12, 1.3m },
                    { 98, 21, 1, 0.75m },
                    { 99, 21, 2, 0.8m },
                    { 100, 21, 4, 1.2m },
                    { 101, 21, 5, 1.3m },
                    { 102, 21, 6, 1.35m },
                    { 103, 21, 7, 1.3m },
                    { 104, 21, 8, 1.25m },
                    { 105, 21, 9, 1.15m },
                    { 106, 21, 12, 0.8m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SEASONAL_PATTERN_MerchantCategoryId_Month",
                table: "SEASONAL_PATTERN",
                columns: new[] { "MerchantCategoryId", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SEASONAL_PATTERN");
        }
    }
}
