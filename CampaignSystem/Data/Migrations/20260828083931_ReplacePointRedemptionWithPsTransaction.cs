using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplacePointRedemptionWithPsTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "POINT_REDEMPTION");

            migrationBuilder.InsertData(
                table: "TRANSACTION_CODE",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[] { 5, "PS", "Puan Harcama" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TRANSACTION_CODE",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.CreateTable(
                name: "POINT_REDEMPTION",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignId = table.Column<int>(type: "int", nullable: false),
                    CardId = table.Column<int>(type: "int", nullable: true),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RedemptionDate = table.Column<DateTime>(type: "datetime2", nullable: false)
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
    }
}
