using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CAMPAIGN",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CampaignType = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MinimumAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaximumAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RewardPoint = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaxRewardAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EarningType = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CAMPAIGN", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MERCHANT",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MerchantNumber = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    MerchantName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MERCHANT", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PRODUCT",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRODUCT", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SEGMENT",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SegmentCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    SegmentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SEGMENT", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TRANSACTION_CODE",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRANSACTION_CODE", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CAMPAIGN_MERCHANT",
                columns: table => new
                {
                    CampaignId = table.Column<int>(type: "int", nullable: false),
                    MerchantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CAMPAIGN_MERCHANT", x => new { x.CampaignId, x.MerchantId });
                    table.ForeignKey(
                        name: "FK_CAMPAIGN_MERCHANT_CAMPAIGN_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "CAMPAIGN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CAMPAIGN_MERCHANT_MERCHANT_MerchantId",
                        column: x => x.MerchantId,
                        principalTable: "MERCHANT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CAMPAIGN_PRODUCT",
                columns: table => new
                {
                    CampaignId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CAMPAIGN_PRODUCT", x => new { x.CampaignId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_CAMPAIGN_PRODUCT_CAMPAIGN_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "CAMPAIGN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CAMPAIGN_PRODUCT_PRODUCT_ProductId",
                        column: x => x.ProductId,
                        principalTable: "PRODUCT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CAMPAIGN_SEGMENT",
                columns: table => new
                {
                    CampaignId = table.Column<int>(type: "int", nullable: false),
                    SegmentId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CAMPAIGN_SEGMENT", x => new { x.CampaignId, x.SegmentId });
                    table.ForeignKey(
                        name: "FK_CAMPAIGN_SEGMENT_CAMPAIGN_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "CAMPAIGN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CAMPAIGN_SEGMENT_SEGMENT_SegmentId",
                        column: x => x.SegmentId,
                        principalTable: "SEGMENT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CUSTOMER",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerNumber = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Gender = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: true),
                    SegmentId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUSTOMER", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CUSTOMER_SEGMENT_SegmentId",
                        column: x => x.SegmentId,
                        principalTable: "SEGMENT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CAMPAIGN_TRANSACTION_CODE",
                columns: table => new
                {
                    CampaignId = table.Column<int>(type: "int", nullable: false),
                    TransactionCodeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CAMPAIGN_TRANSACTION_CODE", x => new { x.CampaignId, x.TransactionCodeId });
                    table.ForeignKey(
                        name: "FK_CAMPAIGN_TRANSACTION_CODE_CAMPAIGN_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "CAMPAIGN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CAMPAIGN_TRANSACTION_CODE_TRANSACTION_CODE_TransactionCodeId",
                        column: x => x.TransactionCodeId,
                        principalTable: "TRANSACTION_CODE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CARD",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    CardType = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CARD", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CARD_CUSTOMER_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CUSTOMER",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CARD_PRODUCT_ProductId",
                        column: x => x.ProductId,
                        principalTable: "PRODUCT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CAMPAIGN_PARTICIPATION",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    CardId = table.Column<int>(type: "int", nullable: true),
                    ParticipationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CAMPAIGN_PARTICIPATION", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CAMPAIGN_PARTICIPATION_CAMPAIGN_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "CAMPAIGN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CAMPAIGN_PARTICIPATION_CARD_CardId",
                        column: x => x.CardId,
                        principalTable: "CARD",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CAMPAIGN_PARTICIPATION_CUSTOMER_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CUSTOMER",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CAMPAIGN_REWARD",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    CardId = table.Column<int>(type: "int", nullable: true),
                    QualifyingCount = table.Column<int>(type: "int", nullable: false),
                    RewardPoint = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RewardDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CAMPAIGN_REWARD", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CAMPAIGN_REWARD_CAMPAIGN_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "CAMPAIGN",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CAMPAIGN_REWARD_CARD_CardId",
                        column: x => x.CardId,
                        principalTable: "CARD",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CAMPAIGN_REWARD_CUSTOMER_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CUSTOMER",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRANSACTION",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Rrn = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: true),
                    CardId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    MerchantId = table.Column<int>(type: "int", nullable: true),
                    TransactionCodeId = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRANSACTION", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TRANSACTION_CARD_CardId",
                        column: x => x.CardId,
                        principalTable: "CARD",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRANSACTION_CUSTOMER_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CUSTOMER",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRANSACTION_MERCHANT_MerchantId",
                        column: x => x.MerchantId,
                        principalTable: "MERCHANT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRANSACTION_TRANSACTION_CODE_TransactionCodeId",
                        column: x => x.TransactionCodeId,
                        principalTable: "TRANSACTION_CODE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CAMPAIGN_Status_EndDate",
                table: "CAMPAIGN",
                columns: new[] { "Status", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CAMPAIGN_MERCHANT_MerchantId",
                table: "CAMPAIGN_MERCHANT",
                column: "MerchantId");

            migrationBuilder.CreateIndex(
                name: "IX_CAMPAIGN_PARTICIPATION_CampaignId_CustomerId_CardId",
                table: "CAMPAIGN_PARTICIPATION",
                columns: new[] { "CampaignId", "CustomerId", "CardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CAMPAIGN_PARTICIPATION_CardId",
                table: "CAMPAIGN_PARTICIPATION",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_CAMPAIGN_PARTICIPATION_CustomerId",
                table: "CAMPAIGN_PARTICIPATION",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CAMPAIGN_PRODUCT_ProductId",
                table: "CAMPAIGN_PRODUCT",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CAMPAIGN_REWARD_CampaignId_CustomerId_CardId",
                table: "CAMPAIGN_REWARD",
                columns: new[] { "CampaignId", "CustomerId", "CardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CAMPAIGN_REWARD_CardId",
                table: "CAMPAIGN_REWARD",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_CAMPAIGN_REWARD_CustomerId",
                table: "CAMPAIGN_REWARD",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CAMPAIGN_SEGMENT_SegmentId",
                table: "CAMPAIGN_SEGMENT",
                column: "SegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CAMPAIGN_TRANSACTION_CODE_TransactionCodeId",
                table: "CAMPAIGN_TRANSACTION_CODE",
                column: "TransactionCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_CARD_CustomerId",
                table: "CARD",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CARD_ProductId",
                table: "CARD",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CUSTOMER_CustomerNumber",
                table: "CUSTOMER",
                column: "CustomerNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CUSTOMER_SegmentId",
                table: "CUSTOMER",
                column: "SegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MERCHANT_MerchantNumber",
                table: "MERCHANT",
                column: "MerchantNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCT_ProductCode",
                table: "PRODUCT",
                column: "ProductCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SEGMENT_SegmentCode",
                table: "SEGMENT",
                column: "SegmentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRANSACTION_CardId_TransactionDate",
                table: "TRANSACTION",
                columns: new[] { "CardId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TRANSACTION_CustomerId_TransactionDate",
                table: "TRANSACTION",
                columns: new[] { "CustomerId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TRANSACTION_MerchantId",
                table: "TRANSACTION",
                column: "MerchantId");

            migrationBuilder.CreateIndex(
                name: "IX_TRANSACTION_Rrn",
                table: "TRANSACTION",
                column: "Rrn",
                unique: true,
                filter: "[Rrn] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TRANSACTION_TransactionCodeId",
                table: "TRANSACTION",
                column: "TransactionCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_TRANSACTION_CODE_Code",
                table: "TRANSACTION_CODE",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CAMPAIGN_MERCHANT");

            migrationBuilder.DropTable(
                name: "CAMPAIGN_PARTICIPATION");

            migrationBuilder.DropTable(
                name: "CAMPAIGN_PRODUCT");

            migrationBuilder.DropTable(
                name: "CAMPAIGN_REWARD");

            migrationBuilder.DropTable(
                name: "CAMPAIGN_SEGMENT");

            migrationBuilder.DropTable(
                name: "CAMPAIGN_TRANSACTION_CODE");

            migrationBuilder.DropTable(
                name: "TRANSACTION");

            migrationBuilder.DropTable(
                name: "CAMPAIGN");

            migrationBuilder.DropTable(
                name: "CARD");

            migrationBuilder.DropTable(
                name: "MERCHANT");

            migrationBuilder.DropTable(
                name: "TRANSACTION_CODE");

            migrationBuilder.DropTable(
                name: "CUSTOMER");

            migrationBuilder.DropTable(
                name: "PRODUCT");

            migrationBuilder.DropTable(
                name: "SEGMENT");
        }
    }
}
