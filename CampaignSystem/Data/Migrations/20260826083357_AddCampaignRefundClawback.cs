using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignRefundClawback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CAMPAIGN_REWARD_CampaignId_CustomerId_CardId",
                table: "CAMPAIGN_REWARD");

            // Existing reward rows are all grants, so they must read as Earn — otherwise they
            // fall outside the filtered unique index and the reconciliation would not see them.
            migrationBuilder.AddColumn<string>(
                name: "RewardType",
                table: "CAMPAIGN_REWARD",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "Earn");

            migrationBuilder.AddColumn<int>(
                name: "RefundClawbackDays",
                table: "CAMPAIGN",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RefundClawbackEnabled",
                table: "CAMPAIGN",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_CAMPAIGN_REWARD_CampaignId_CustomerId_CardId",
                table: "CAMPAIGN_REWARD",
                columns: new[] { "CampaignId", "CustomerId", "CardId" },
                unique: true,
                filter: "[RewardType] = 'Earn'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CAMPAIGN_REWARD_CampaignId_CustomerId_CardId",
                table: "CAMPAIGN_REWARD");

            migrationBuilder.DropColumn(
                name: "RewardType",
                table: "CAMPAIGN_REWARD");

            migrationBuilder.DropColumn(
                name: "RefundClawbackDays",
                table: "CAMPAIGN");

            migrationBuilder.DropColumn(
                name: "RefundClawbackEnabled",
                table: "CAMPAIGN");

            migrationBuilder.CreateIndex(
                name: "IX_CAMPAIGN_REWARD_CampaignId_CustomerId_CardId",
                table: "CAMPAIGN_REWARD",
                columns: new[] { "CampaignId", "CustomerId", "CardId" },
                unique: true);
        }
    }
}
