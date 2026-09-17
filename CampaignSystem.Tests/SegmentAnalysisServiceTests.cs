using CampaignSystem.Configuration;
using CampaignSystem.Data;
using CampaignSystem.Entities;
using CampaignSystem.Enums;
using CampaignSystem.Services;
using CampaignSystem.Services.Advisor;
using CampaignSystem.Tests.Infrastructure;
using Microsoft.Extensions.Options;

namespace CampaignSystem.Tests;

/// <summary>
/// The segment breakdown is what the AI advisor reads a segment's habits from, so a wrong
/// figure here turns into a confident sentence in front of the business unit. These tests pin
/// the definition of spend — sales net of refunds, never cash advances or debt payments — the
/// index against the bank-wide share, and the small-cell rule that withholds ratios.
///
/// Same pattern as the recommendation tests: each test builds its own data inside a
/// transaction that is rolled back, with dates relative to now.
/// </summary>
public class SegmentAnalysisServiceTests(TestDatabaseFixture fixture)
    : IClassFixture<TestDatabaseFixture>
{
    private const int FarmerSegmentId = 3;
    private const int RetiredSegmentId = 5;
    private const int VisaGoldProductId = 3;
    private const int SaleCodeId = 1;
    private const int CashAdvanceCodeId = 2;
    private const int DebtPaymentCodeId = 3;
    private const int RefundCodeId = 4;

    private static int _sequence;

    private static SegmentAnalysisService CreateService(CampaignDbContext context, int minCellCustomers = 1)
        => new(context, Options.Create(new AdvisorOptions { SegmentMinCellCustomers = minCellCustomers }));

    [Fact]
    public async Task CountsSalesNetOfRefunds_ButNotCashAdvancesOrDebtPayments()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (categoryId, merchantId) = await AddCategoryWithMerchantAsync(context);
        var card = await AddCustomerWithCardAsync(context, FarmerSegmentId);
        var date = DateTime.Now.AddDays(-10);

        // Cash advance and debt payment rows carry a merchant here on purpose, so it is the
        // transaction code — not a missing merchant — that has to keep them out.
        var sale = Row(card, merchantId, SaleCodeId, 1_000m, date);
        context.Transactions.AddRange(
            sale,
            Row(card, merchantId, CashAdvanceCodeId, 5_000m, date),
            Row(card, merchantId, DebtPaymentCodeId, 3_000m, date));
        await context.SaveChangesAsync();

        var refund = Row(card, merchantId, RefundCodeId, -200m, date.AddDays(2));
        refund.OriginalTransactionId = sale.Id;
        context.Transactions.Add(refund);
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetCategoryBreakdownAsync(FarmerSegmentId);

        var cell = Assert.Single(result.Value!.Categories, c => c.MerchantCategoryId == categoryId);
        Assert.True(cell.EnoughData);
        Assert.Equal(800m, cell.NetSpend);
        Assert.Equal(1, cell.PurchaseCount);
        Assert.Equal(1_000m, cell.AverageTicket);
    }

    [Fact]
    public async Task Index_ComparesTheSegmentsShare_WithTheBankWideShare()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (fuelId, fuelMerchant) = await AddCategoryWithMerchantAsync(context);
        var (pharmacyId, pharmacyMerchant) = await AddCategoryWithMerchantAsync(context);
        var farmer = await AddCustomerWithCardAsync(context, FarmerSegmentId);
        var retired = await AddCustomerWithCardAsync(context, RetiredSegmentId);
        var date = DateTime.Now.AddDays(-10);

        // Large enough to dominate anything else in the window: the farmer puts 90% of their
        // spend into fuel, the retired customer 90% into the pharmacy.
        context.Transactions.AddRange(
            Row(farmer, fuelMerchant, SaleCodeId, 900_000m, date),
            Row(farmer, pharmacyMerchant, SaleCodeId, 100_000m, date),
            Row(retired, fuelMerchant, SaleCodeId, 100_000m, date),
            Row(retired, pharmacyMerchant, SaleCodeId, 900_000m, date));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetCategoryBreakdownAsync(FarmerSegmentId);

        var categories = result.Value!.Categories.ToList();
        var fuel = Assert.Single(categories, c => c.MerchantCategoryId == fuelId);
        var pharmacy = Assert.Single(categories, c => c.MerchantCategoryId == pharmacyId);

        Assert.True(fuel.Index > 1.0);
        Assert.True(pharmacy.Index < 1.0);
        Assert.True(categories.IndexOf(fuel) < categories.IndexOf(pharmacy));
    }

    [Fact]
    public async Task WithholdsTheRatios_OfACellWithTooFewBuyers()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (categoryId, merchantId) = await AddCategoryWithMerchantAsync(context);
        var card = await AddCustomerWithCardAsync(context, FarmerSegmentId);
        context.Transactions.Add(Row(card, merchantId, SaleCodeId, 2_500m, DateTime.Now.AddDays(-5)));
        await context.SaveChangesAsync();

        var result = await CreateService(context, minCellCustomers: 20)
            .GetCategoryBreakdownAsync(FarmerSegmentId);

        var cell = Assert.Single(result.Value!.Categories, c => c.MerchantCategoryId == categoryId);
        Assert.False(cell.EnoughData);
        Assert.Equal(1, cell.Buyers);
        Assert.Null(cell.NetSpend);
        Assert.Null(cell.Index);
        Assert.Null(cell.Penetration);
    }

    [Fact]
    public async Task ReturnsNotFound_ForAnUnknownSegment()
    {
        await using var context = fixture.CreateContext();

        var result = await CreateService(context).GetCategoryBreakdownAsync(999_999);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    private static async Task<(int CategoryId, int MerchantId)> AddCategoryWithMerchantAsync(
        CampaignDbContext context)
    {
        var suffix = Interlocked.Increment(ref _sequence);

        var category = new MerchantCategory
        {
            CategoryCode = $"SG{suffix:D4}",
            CategoryName = $"Segment Test Category {suffix}"
        };
        context.MerchantCategories.Add(category);
        await context.SaveChangesAsync();

        var merchant = new Merchant
        {
            MerchantNumber = $"SGM{suffix:D6}",
            MerchantName = $"Segment Test Merchant {suffix}",
            IsActive = true,
            MerchantCategoryId = category.Id
        };
        context.Merchants.Add(merchant);
        await context.SaveChangesAsync();

        return (category.Id, merchant.Id);
    }

    private static async Task<Card> AddCustomerWithCardAsync(CampaignDbContext context, int segmentId)
    {
        var suffix = Interlocked.Increment(ref _sequence);

        var card = new Card
        {
            Customer = new Customer
            {
                CustomerNumber = $"SG{suffix:D10}",
                Gender = Gender.Male,
                SegmentId = segmentId,
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

    private static Transaction Row(Card card, int merchantId, int codeId, decimal amount, DateTime date) => new()
    {
        CardId = card.Id,
        CustomerId = card.CustomerId,
        MerchantId = merchantId,
        TransactionCodeId = codeId,
        TransactionDate = date,
        Amount = amount
    };
}
