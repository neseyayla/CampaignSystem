using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CampaignSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUnusedPointsClawbackToCampaign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UnusedPointsClawbackDays",
                table: "CAMPAIGN",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UnusedPointsClawbackEnabled",
                table: "CAMPAIGN",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UnusedPointsClawbackProcessedAt",
                table: "CAMPAIGN",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CAMPAIGN_CLAWBACK_EXEMPT_PRODUCT",
                columns: table => new
                {
                    CampaignId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CAMPAIGN_CLAWBACK_EXEMPT_PRODUCT", x => new { x.CampaignId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_CAMPAIGN_CLAWBACK_EXEMPT_PRODUCT_CAMPAIGN_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "CAMPAIGN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CAMPAIGN_CLAWBACK_EXEMPT_PRODUCT_PRODUCT_ProductId",
                        column: x => x.ProductId,
                        principalTable: "PRODUCT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "POINT_REDEMPTION",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    CardId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RedemptionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POINT_REDEMPTION", x => x.Id);
                    table.ForeignKey(
                        name: "FK_POINT_REDEMPTION_CAMPAIGN_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "CAMPAIGN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POINT_REDEMPTION_CARD_CardId",
                        column: x => x.CardId,
                        principalTable: "CARD",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POINT_REDEMPTION_CUSTOMER_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CUSTOMER",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CAMPAIGN_CONDITION_TEMPLATE",
                columns: new[] { "Id", "IsActive", "Key", "TemplateText" },
                values: new object[,]
                {
                    { 14, true, "UnusedPointsClawback", "Bu kampanyadan kazanılan kullanılmayan Worldpuanlar {ReclaimDate} tarihinde geri alınır." },
                    { 15, true, "UnusedPointsClawbackExempt", "Şu kart tipleri bu geri alım kuralından muaftır: {Names}." }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CAMPAIGN_CLAWBACK_EXEMPT_PRODUCT_ProductId",
                table: "CAMPAIGN_CLAWBACK_EXEMPT_PRODUCT",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_POINT_REDEMPTION_CampaignId_CustomerId_CardId",
                table: "POINT_REDEMPTION",
                columns: new[] { "CampaignId", "CustomerId", "CardId" });

            migrationBuilder.CreateIndex(
                name: "IX_POINT_REDEMPTION_CardId",
                table: "POINT_REDEMPTION",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_POINT_REDEMPTION_CustomerId",
                table: "POINT_REDEMPTION",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CAMPAIGN_CLAWBACK_EXEMPT_PRODUCT");

            migrationBuilder.DropTable(
                name: "POINT_REDEMPTION");

            migrationBuilder.DeleteData(
                table: "CAMPAIGN_CONDITION_TEMPLATE",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "CAMPAIGN_CONDITION_TEMPLATE",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DropColumn(
                name: "UnusedPointsClawbackDays",
                table: "CAMPAIGN");

            migrationBuilder.DropColumn(
                name: "UnusedPointsClawbackEnabled",
                table: "CAMPAIGN");

            migrationBuilder.DropColumn(
                name: "UnusedPointsClawbackProcessedAt",
                table: "CAMPAIGN");
        }
    }
}
