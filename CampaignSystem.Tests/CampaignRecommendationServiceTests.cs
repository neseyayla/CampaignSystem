using CampaignSystem.Configuration;
using CampaignSystem.Data;
using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Enums;
using CampaignSystem.Services;
using CampaignSystem.Tests.Infrastructure;
using Microsoft.Extensions.Options;

namespace CampaignSystem.Tests;

/// <summary>
/// The recommendation engine turns raw transaction history into ranked campaign ideas, and
/// the ranking is the whole product: a wrong order sends an operator to define the wrong
/// campaign. These tests pin the behaviours that decide that order — spend nets out refunds,
/// a rising category beats a flat one, a category a campaign already covers drops out, and a
/// season ahead lifts the score — against a real database so the grouped SQL is exercised too.
///
/// Each test runs inside a transaction that is rolled back, and builds its own service, so
/// the tests share no state and their order does not matter. Dates are relative to now so no
/// test starts failing on a particular calendar day.
/// </summary>
public class CampaignRecommendationServiceTests(TestDatabaseFixture fixture)
    : IClassFixture<TestDatabaseFixture>
{
    private const int FarmerSegmentId = 3;
    private const int VisaGoldProductId = 3;
    private const int SaleTransactionCodeId = 1;

    private static int _sequence;

    private static CampaignRecommendationService CreateService(
        CampaignDbContext context, RecommendationOptions? options = null)
        => new(context, Options.Create(options ?? new RecommendationOptions { MinimumSpend = 100m }));

    [Fact]
    public async Task Suggests_ABusyCategory_NoCampaignCovers()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (categoryId, merchantId) = await AddCategoryWithMerchantAsync(context);
        var card = await AddCustomerWithCardAsync(context);

        var now = DateTime.Now;
        context.Transactions.AddRange(
            Purchase(card, merchantId, 4_000m, now.AddDays(-20)),
            Purchase(card, merchantId, 3_500m, now.AddDays(-10)),
            Purchase(card, merchantId, 2_500m, now.AddDays(-3)));
        await context.SaveChangesAsync();

        var suggestions = await CreateService(context).GetSuggestionsAsync(new RecommendationQueryDto());

        var suggestion = Assert.Single(suggestions, s => s.MerchantCategoryId == categoryId);
        Assert.Equal(1, suggestion.Rank);
        Assert.True(suggestion.Reason.IsCoverageGap);
        Assert.Equal(10_000m, suggestion.Reason.TotalSpend);
        Assert.Equal(3, suggestion.Reason.TransactionCount);
        Assert.Contains(merchantId, suggestion.Draft.MerchantIds);
        Assert.Equal(categoryId, suggestion.Draft.MerchantCategoryId);
        Assert.True(suggestion.Draft.SuggestedRewardPoint >= 1m);
    }

    [Fact]
    public async Task Excludes_ACategory_AnOpenCampaignAlreadyCovers()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (categoryId, merchantId) = await AddCategoryWithMerchantAsync(context);
        var card = await AddCustomerWithCardAsync(context);

        var now = DateTime.Now;
        context.Transactions.AddRange(
            Purchase(card, merchantId, 5_000m, now.AddDays(-15)),
            Purchase(card, merchantId, 5_000m, now.AddDays(-5)));

        var campaign = new Campaign
        {
            Name = "Existing",
            CampaignType = CampaignType.Mass,
            EarningType = EarningType.CustomerBased,
            StartDate = now.AddDays(-10),
            EndDate = now.AddDays(20),
            Status = CampaignStatus.Ongoing,
            IsActive = true,
            CampaignMerchants = [new CampaignMerchant { MerchantId = merchantId }]
        };
        context.Campaigns.Add(campaign);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var withoutCovered = await service.GetSuggestionsAsync(new RecommendationQueryDto());
        Assert.DoesNotContain(withoutCovered, s => s.MerchantCategoryId == categoryId);

        var withCovered = await service.GetSuggestionsAsync(new RecommendationQueryDto { IncludeCovered = true });
        var suggestion = Assert.Single(withCovered, s => s.MerchantCategoryId == categoryId);
        Assert.False(suggestion.Reason.IsCoverageGap);
        Assert.Contains(campaign.Id, suggestion.Reason.CoveringCampaignIds);
    }

    [Fact]
    public async Task NetsRefundsOut_OfTheSpendItScores()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (categoryId, merchantId) = await AddCategoryWithMerchantAsync(context);
        var card = await AddCustomerWithCardAsync(context);

        var now = DateTime.Now;
        var purchase = Purchase(card, merchantId, 10_000m, now.AddDays(-12));
        context.Transactions.Add(purchase);
        await context.SaveChangesAsync();

        // A refund row is stored as a negative amount pointing at the original purchase.
        context.Transactions.Add(new Transaction
        {
            CardId = card.Id,
            CustomerId = card.CustomerId,
            MerchantId = merchantId,
            TransactionCodeId = SaleTransactionCodeId,
            TransactionDate = now.AddDays(-6),
            Amount = -6_000m,
            OriginalTransactionId = purchase.Id
        });
        await context.SaveChangesAsync();

        var suggestions = await CreateService(context).GetSuggestionsAsync(new RecommendationQueryDto());

        var suggestion = Assert.Single(suggestions, s => s.MerchantCategoryId == categoryId);
        Assert.Equal(4_000m, suggestion.Reason.TotalSpend);
        // Only the purchase is a real transaction; the refund is not counted as one.
        Assert.Equal(1, suggestion.Reason.TransactionCount);
    }

    [Fact]
    public async Task ReportsARisingTrend_InTheReasonAndHeadline()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (categoryId, merchantId) = await AddCategoryWithMerchantAsync(context);
        var card = await AddCustomerWithCardAsync(context);

        var now = DateTime.Now;
        // Lookback 40 days -> the halves split at now-20. Little before it, a lot after.
        context.Transactions.AddRange(
            Purchase(card, merchantId, 1_000m, now.AddDays(-30)),
            Purchase(card, merchantId, 4_000m, now.AddDays(-8)),
            Purchase(card, merchantId, 4_000m, now.AddDays(-2)));
        await context.SaveChangesAsync();

        var options = new RecommendationOptions { MinimumSpend = 100m, LookbackDays = 40 };
        var suggestions = await CreateService(context, options)
            .GetSuggestionsAsync(new RecommendationQueryDto());

        var suggestion = Assert.Single(suggestions, s => s.MerchantCategoryId == categoryId);
        Assert.NotNull(suggestion.Reason.TrendRatio);
        Assert.True(suggestion.Reason.TrendRatio > 0.5);
        Assert.Contains("arttı", suggestion.Headline);
    }

    [Fact]
    public async Task RanksASeasonalCategory_AboveANeutralOne_AtEqualSpend()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (seasonalCategoryId, seasonalMerchantId) = await AddCategoryWithMerchantAsync(context);
        var (neutralCategoryId, neutralMerchantId) = await AddCategoryWithMerchantAsync(context);

        // A pronounced season in every month, so the result does not depend on today's date.
        for (var month = 1; month <= 12; month++)
        {
            context.SeasonalPatterns.Add(new SeasonalPattern
            {
                MerchantCategoryId = seasonalCategoryId,
                Month = month,
                Weight = 1.6m
            });
        }

        var card = await AddCustomerWithCardAsync(context);
        var now = DateTime.Now;
        context.Transactions.AddRange(
            Purchase(card, seasonalMerchantId, 6_000m, now.AddDays(-9)),
            Purchase(card, neutralMerchantId, 6_000m, now.AddDays(-9)));
        await context.SaveChangesAsync();

        var suggestions = await CreateService(context).GetSuggestionsAsync(new RecommendationQueryDto());

        var seasonal = Assert.Single(suggestions, s => s.MerchantCategoryId == seasonalCategoryId);
        var neutral = Assert.Single(suggestions, s => s.MerchantCategoryId == neutralCategoryId);
        Assert.True(seasonal.Reason.SeasonalWeight > 1.5);
        Assert.Equal(1.0, neutral.Reason.SeasonalWeight);
        Assert.True(seasonal.Score > neutral.Score);
        Assert.True(seasonal.Rank < neutral.Rank);
    }

    [Fact]
    public async Task ReturnsEmpty_WhenNoSpendFallsInsideTheWindow()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (_, merchantId) = await AddCategoryWithMerchantAsync(context);
        var card = await AddCustomerWithCardAsync(context);

        // Comfortably older than the 90-day default lookback.
        context.Transactions.Add(Purchase(card, merchantId, 20_000m, DateTime.Now.AddDays(-200)));
        await context.SaveChangesAsync();

        var suggestions = await CreateService(context).GetSuggestionsAsync(new RecommendationQueryDto());

        Assert.Empty(suggestions);
    }

    private static async Task<(int CategoryId, int MerchantId)> AddCategoryWithMerchantAsync(
        CampaignDbContext context)
    {
        var suffix = Interlocked.Increment(ref _sequence);

        var category = new MerchantCategory
        {
            CategoryCode = $"TC{suffix:D4}",
            CategoryName = $"Test Category {suffix}"
        };
        context.MerchantCategories.Add(category);
        await context.SaveChangesAsync();

        var merchant = new Merchant
        {
            MerchantNumber = $"TM{suffix:D6}",
            MerchantName = $"Test Merchant {suffix}",
            IsActive = true,
            MerchantCategoryId = category.Id
        };
        context.Merchants.Add(merchant);
        await context.SaveChangesAsync();

        return (category.Id, merchant.Id);
    }

    private static async Task<Card> AddCustomerWithCardAsync(CampaignDbContext context)
    {
        var suffix = Interlocked.Increment(ref _sequence);

        var card = new Card
        {
            Customer = new Customer
            {
                CustomerNumber = $"TR{suffix:D10}",
                Gender = Gender.Female,
                SegmentId = FarmerSegmentId,
                IsActive = true
            },
            ProductId = VisaGoldProductId,
            CardType = CardType.Primary,
            IsActive = true
        };
        context.Cards.Add(card);
        await context.SaveChangesAsync();

        return card;
    }

    private static Transaction Purchase(Card card, int merchantId, decimal amount, DateTime date) => new()
    {
        CardId = card.Id,
        CustomerId = card.CustomerId,
        MerchantId = merchantId,
        TransactionCodeId = SaleTransactionCodeId,
        TransactionDate = date,
        Amount = amount
    };
}
