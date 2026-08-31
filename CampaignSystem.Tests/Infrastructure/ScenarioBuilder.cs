using CampaignSystem.Data;
using CampaignSystem.Entities;
using CampaignSystem.Enums;

namespace CampaignSystem.Tests.Infrastructure;

/// <summary>
/// Builds the small, hand-checkable data set the reward tests share.
///
/// Reference data (segments, products, merchants, transaction codes) already exists — the
/// seed migration puts it there — so only the customer, cards and transactions are created
/// here. Ids of the seeded rows are stated as constants rather than looked up, because the
/// seed fixes them deliberately for exactly this reason.
/// </summary>
public static class ScenarioBuilder
{
    public const int FarmerSegmentId = 3;
    public const int VisaGoldProductId = 3;
    public const int MasterCardGoldProductId = 4;
    public const int OpetMerchantId = 3;
    public const int SaleTransactionCodeId = 1;
    public const int CashAdvanceTransactionCodeId = 2;

    /// <summary>The "PS" code — a transaction that spends campaign points. Seed id.</summary>
    public const int RedemptionTransactionCodeId = 5;

    /// <summary>
    /// A campaign that has already finished, so the reward batch will accept it.
    ///
    /// Dates are relative to now rather than fixed, so the tests do not start failing on a
    /// particular calendar date.
    /// </summary>
    public static Campaign FinishedCampaign(
        EarningType earningType,
        decimal? maxRewardAmount = null,
        decimal rewardPoint = 50m,
        decimal? minimumAmount = 250m) => new()
    {
        Name = "Test campaign",
        CampaignType = CampaignType.Mass,
        EarningType = earningType,
        StartDate = DateTime.Now.AddDays(-60),
        EndDate = DateTime.Now.AddDays(-30),
        MinimumAmount = minimumAmount,
        RewardPoint = rewardPoint,
        MaxRewardAmount = maxRewardAmount,
        Status = CampaignStatus.Loading,
        IsActive = true
    };

    /// <summary>
    /// One farmer with two Gold cards, and seven transactions of which five qualify for a
    /// campaign restricted to sales over 250 at Opet:
    ///
    ///   card A — three qualifying sales
    ///   card B — two qualifying sales
    ///   card A — one sale of 100, under any 250 minimum
    ///   card A — one qualifying-looking sale dated before the campaign started
    ///
    /// So a card based campaign paying 50 a transaction owes 150 on card A and 100 on card B;
    /// a customer based one owes 250 in a single row.
    /// </summary>
    public static async Task<Scenario> CreateAsync(CampaignDbContext context, Campaign campaign)
    {
        var suffix = DateTime.Now.Ticks.ToString()[^10..];

        var customer = new Customer
        {
            CustomerNumber = $"T{suffix}",
            Gender = Gender.Female,
            SegmentId = FarmerSegmentId,
            IsActive = true
        };

        var cardA = new Card
        {
            Customer = customer,
            ProductId = VisaGoldProductId,
            CardType = CardType.Primary,
            IsActive = true
        };

        var cardB = new Card
        {
            Customer = customer,
            ProductId = MasterCardGoldProductId,
            CardType = CardType.Supplementary,
            IsActive = true
        };

        context.Campaigns.Add(campaign);
        context.Customers.Add(customer);
        context.Cards.AddRange(cardA, cardB);

        // Ids are needed below, and the transactions reference them.
        await context.SaveChangesAsync();

        var insideCampaign = campaign.StartDate.AddDays(1);
        var beforeCampaign = campaign.StartDate.AddDays(-5);

        var rrn = 0;

        context.Transactions.AddRange(
            // Card A — three qualifying
            Transaction(cardA, 300m, insideCampaign),
            Transaction(cardA, 500m, insideCampaign.AddDays(2)),
            Transaction(cardA, 250m, insideCampaign.AddDays(4)),

            // Card B — two qualifying
            Transaction(cardB, 400m, insideCampaign.AddDays(1)),
            Transaction(cardB, 900m, insideCampaign.AddDays(3)),

            // Under the minimum
            Transaction(cardA, 100m, insideCampaign.AddDays(5)),

            // Right shape, wrong period
            Transaction(cardA, 600m, beforeCampaign));

        await context.SaveChangesAsync();

        return new Scenario(campaign, customer, cardA, cardB);

        Transaction Transaction(Card card, decimal amount, DateTime date) => new()
        {
            Rrn = $"R{suffix}{rrn++:D2}",
            CardId = card.Id,
            CustomerId = customer.Id,
            MerchantId = OpetMerchantId,
            TransactionCodeId = SaleTransactionCodeId,
            TransactionDate = date,
            Amount = amount
        };
    }

    public record Scenario(Campaign Campaign, Customer Customer, Card CardA, Card CardB);
}
