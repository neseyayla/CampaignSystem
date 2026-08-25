using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignEnrollmentBasis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnrollmentBasis",
                table: "CAMPAIGN",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnrollmentBasis",
                table: "CAMPAIGN");
        }
    }
}
