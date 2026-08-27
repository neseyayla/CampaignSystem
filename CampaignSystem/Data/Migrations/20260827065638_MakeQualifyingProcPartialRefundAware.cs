using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeQualifyingProcPartialRefundAware : Migration
    {
        // Keep the diagnostic proc in step with RewardService now that refunds can be partial:
        // a purchase counts on its amount net of refunds. Maximum is tested on the original
        // amount; minimum and being non-zero are tested on the remainder.
        private const string PartialRefundAware = @"
CREATE OR ALTER PROCEDURE dbo.usp_CampaignQualifyingTransactions
    @CampaignId INT,
    @CustomerId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Start DATETIME2, @End DATETIME2, @Type VARCHAR(10),
            @MinAmt DECIMAL(18,2), @MaxAmt DECIMAL(18,2),
            @Gender VARCHAR(1), @CardType VARCHAR(1);

    SELECT @Start = StartDate, @End = EndDate, @Type = CampaignType,
           @MinAmt = MinimumAmount, @MaxAmt = MaximumAmount,
           @Gender = Gender, @CardType = CardType
    FROM dbo.CAMPAIGN WHERE Id = @CampaignId;

    IF @Start IS NULL
    BEGIN
        RAISERROR('Campaign %d not found.', 16, 1, @CampaignId);
        RETURN;
    END

    DECLARE @Now DATETIME2 = GETDATE();

    ;WITH base AS (
        SELECT t.Id, t.Rrn, t.CardId, t.CustomerId, t.MerchantId,
               t.TransactionCodeId, t.TransactionDate, t.Amount, c.ProductId,
               -- Amount net of every refund that points at this purchase (refund rows are negative).
               t.Amount + ISNULL((SELECT SUM(rf.Amount) FROM dbo.[TRANSACTION] rf
                                  WHERE rf.OriginalTransactionId = t.Id), 0) AS Effective
        FROM dbo.[TRANSACTION] t
        JOIN dbo.CARD     c  ON c.Id  = t.CardId
        JOIN dbo.CUSTOMER cu ON cu.Id = t.CustomerId
        WHERE t.TransactionDate >= @Start
          AND t.TransactionDate <= @End
          AND t.TransactionDate <= @Now
          -- A refund row itself never earns.
          AND t.OriginalTransactionId IS NULL
          AND (@CustomerId IS NULL OR t.CustomerId = @CustomerId)
          -- Maximum on the original amount; a partial refund cannot rescue a too-large purchase.
          AND (@MaxAmt IS NULL OR t.Amount <= @MaxAmt)
          AND (@Gender IS NULL OR cu.Gender = @Gender)
          AND (@CardType IS NULL OR c.CardType = @CardType)
          AND (NOT EXISTS (SELECT 1 FROM dbo.CAMPAIGN_MERCHANT WHERE CampaignId = @CampaignId)
               OR (t.MerchantId IS NOT NULL
                   AND EXISTS (SELECT 1 FROM dbo.CAMPAIGN_MERCHANT m
                               WHERE m.CampaignId = @CampaignId AND m.MerchantId = t.MerchantId)))
          AND (NOT EXISTS (SELECT 1 FROM dbo.CAMPAIGN_TRANSACTION_CODE WHERE CampaignId = @CampaignId)
               OR EXISTS (SELECT 1 FROM dbo.CAMPAIGN_TRANSACTION_CODE tc
                          WHERE tc.CampaignId = @CampaignId AND tc.TransactionCodeId = t.TransactionCodeId))
          AND (NOT EXISTS (SELECT 1 FROM dbo.CAMPAIGN_PRODUCT WHERE CampaignId = @CampaignId)
               OR EXISTS (SELECT 1 FROM dbo.CAMPAIGN_PRODUCT pr
                          WHERE pr.CampaignId = @CampaignId AND pr.ProductId = c.ProductId))
          AND (NOT EXISTS (SELECT 1 FROM dbo.CAMPAIGN_SEGMENT WHERE CampaignId = @CampaignId)
               OR (cu.SegmentId IS NOT NULL
                   AND EXISTS (SELECT 1 FROM dbo.CAMPAIGN_SEGMENT s
                               WHERE s.CampaignId = @CampaignId AND s.SegmentId = cu.SegmentId)))
    ),
    cust_enroll AS (
        SELECT CustomerId, MIN(ParticipationDate) AS FromDate
        FROM dbo.CAMPAIGN_PARTICIPATION
        WHERE CampaignId = @CampaignId AND [Status] = 'Active' AND CardId IS NULL
        GROUP BY CustomerId
    ),
    card_enroll AS (
        SELECT CardId, MIN(ParticipationDate) AS FromDate
        FROM dbo.CAMPAIGN_PARTICIPATION
        WHERE CampaignId = @CampaignId AND [Status] = 'Active' AND CardId IS NOT NULL
        GROUP BY CardId
    )
    SELECT b.Id AS TransactionId, b.Rrn, b.TransactionDate, b.Amount, b.Effective,
           b.CustomerId, b.CardId, b.MerchantId, b.TransactionCodeId
    FROM base b
    -- Minimum, and being non-zero, on the amount net of refunds.
    WHERE b.Effective > 0
      AND (@MinAmt IS NULL OR b.Effective >= @MinAmt)
      AND (@Type = 'MASS'
           OR EXISTS (SELECT 1 FROM cust_enroll ce
                      WHERE ce.CustomerId = b.CustomerId AND b.TransactionDate >= ce.FromDate)
           OR EXISTS (SELECT 1 FROM card_enroll ka
                      WHERE ka.CardId = b.CardId AND b.TransactionDate >= ka.FromDate))
    ORDER BY b.CustomerId, b.CardId, b.TransactionDate;
END";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PartialRefundAware);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: making the diagnostic proc partial-refund aware is a correctness fix; rolling
            // back does not need to reintroduce the full-refund-only form.
        }
    }
}
