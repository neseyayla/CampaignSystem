using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CampaignSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignConditionTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CAMPAIGN_CONDITION_TEMPLATE",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    TemplateText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CAMPAIGN_CONDITION_TEMPLATE", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CAMPAIGN_CONDITION_TEMPLATE",
                columns: new[] { "Id", "IsActive", "Key", "TemplateText" },
                values: new object[,]
                {
                    { 1, true, "DateRange", "Kampanya {StartDate} - {EndDate} tarihleri arasında geçerlidir." },
                    { 2, true, "EnrollmentRequired", "Kampanyadan yararlanabilmek için kampanyaya katılım sağlanması gerekmektedir." },
                    { 3, true, "MinAndMaxAmount", "İşlem tutarı {MinimumAmount} TL ile {MaximumAmount} TL arasında olmalıdır." },
                    { 4, true, "MinAmountOnly", "En az {MinimumAmount} TL tutarında işlem yapılması gerekmektedir." },
                    { 5, true, "MaxAmountOnly", "İşlem tutarı en fazla {MaximumAmount} TL olmalıdır." },
                    { 6, true, "RewardPoint", "Uygun her işlem için {RewardPoint} TL Worldpuan kazandırır." },
                    { 7, true, "MaxRewardAmount", "Kampanya kapsamında {PerUnit} başına en fazla {MaxRewardAmount} TL Worldpuan kazanılabilir." },
                    { 8, true, "Gender", "Kampanya yalnızca {GenderText} müşteriler için geçerlidir." },
                    { 9, true, "CardType", "Kampanya yalnızca {CardTypeText} kartlar için geçerlidir." },
                    { 10, true, "SegmentList", "Kampanya yalnızca şu müşteri gruplarına açıktır: {Names}." },
                    { 11, true, "ProductList", "Kampanya yalnızca şu kart tiplerinde geçerlidir: {Names}." },
                    { 12, true, "MerchantList", "Kampanya yalnızca şu üye işyerlerinde yapılan alışverişlerde geçerlidir: {Names}." },
                    { 13, true, "TransactionCodeList", "Kampanya yalnızca şu işlem türlerinde geçerlidir: {Names}." }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CAMPAIGN_CONDITION_TEMPLATE_Key",
                table: "CAMPAIGN_CONDITION_TEMPLATE",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CAMPAIGN_CONDITION_TEMPLATE");
        }
    }
}
