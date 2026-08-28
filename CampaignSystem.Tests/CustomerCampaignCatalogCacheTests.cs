using CampaignSystem.Configuration;
using CampaignSystem.Data;
using CampaignSystem.Entities;
using CampaignSystem.Enums;
using CampaignSystem.Repositories;
using CampaignSystem.Services;
using CampaignSystem.Services.Caching;
using CampaignSystem.Tests.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CampaignSystem.Tests;

/// <summary>
/// The customer campaign list caches its person-independent half — the open campaigns and
/// their criteria and terms — and layers eligibility and enrolment on top per request. These
/// tests pin the two halves of that split: the shared catalog is served from memory and only
/// a write (here, an explicit invalidation) refreshes it, while the per-customer part is never
/// cached, so an enrolment shows on the very next read even though the catalog did not move.
///
/// Each test runs inside a transaction that is rolled back and builds its own cache, so the
/// tests share no state.
/// </summary>
public class CustomerCampaignCatalogCacheTests(TestDatabaseFixture fixture)
    : IClassFixture<TestDatabaseFixture>
{
    private const int FarmerSegmentId = 3;
    private const int VisaGoldProductId = 3;

    private static int _sequence;

    private static CustomerCampaignService CreateService(
        CampaignDbContext context, CampaignCatalogCache cache) =>
        new(context,
            new ParticipationService(
                new Repository<CampaignParticipation>(context),
                new Repository<Campaign>(context),
                new Repository<Customer>(context),
                new Repository<Card>(context)),
            new RewardService(context, Options.Create(new RewardCalculationOptions())),
            cache);

    [Fact]
    public async Task Catalog_IsServedFromCache_AndRebuiltAfterInvalidate()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var customer = NewCustomer();
        var campaignA = NewOngoingCampaign();
        context.AddRange(customer, campaignA);
        await context.SaveChangesAsync();

        context.Add(NewCard(customer));
        await context.SaveChangesAsync();

        var cache = new CampaignCatalogCache(new MemoryCache(new MemoryCacheOptions()));
        var service = CreateService(context, cache);

        // First read builds and caches the catalog; campaign A is on offer.
        var first = await service.GetEligibleAsync(customer.Id);
        Assert.Contains(first!, c => c.CampaignId == campaignA.Id);

        // A second open campaign inserted behind the service does not evict the catalog, so a
        // cached read must not see it yet.
        var campaignB = NewOngoingCampaign();
        context.Add(campaignB);
        await context.SaveChangesAsync();

        var second = await service.GetEligibleAsync(customer.Id);
        Assert.DoesNotContain(second!, c => c.CampaignId == campaignB.Id);

        // Once the catalog is invalidated — as every campaign write does — the next read
        // rebuilds it from the database and campaign B appears.
        cache.Invalidate();

        var third = await service.GetEligibleAsync(customer.Id);
        Assert.Contains(third!, c => c.CampaignId == campaignB.Id);
    }

    [Fact]
    public async Task Enrolment_ShowsImmediately_EvenWhileTheCatalogIsCached()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var customer = NewCustomer();
        var campaign = NewOngoingCampaign(CampaignType.EnrollmentRequired);
        context.AddRange(customer, campaign);
        await context.SaveChangesAsync();

        var card = NewCard(customer);
        context.Add(card);
        await context.SaveChangesAsync();

        var cache = new CampaignCatalogCache(new MemoryCache(new MemoryCacheOptions()));
        var service = CreateService(context, cache);

        // Read once so the catalog is cached with the customer not yet enrolled.
        var before = (await service.GetEligibleAsync(customer.Id))!.Single(c => c.CampaignId == campaign.Id);
        Assert.False(before.Enrolled);

        var enrolled = await service.EnrollAsync(customer.Id, campaign.Id, card.Id);
        Assert.Equal(ResultStatus.Success, enrolled.Status);

        // The catalog was never invalidated, yet the enrolment shows: the Enrolled flag comes
        // from the per-customer query, which runs every request.
        var after = (await service.GetEligibleAsync(customer.Id))!.Single(c => c.CampaignId == campaign.Id);
        Assert.True(after.Enrolled);
    }

    private static Campaign NewOngoingCampaign(CampaignType campaignType = CampaignType.Mass) => new()
    {
        Name = "Catalog cache test campaign",
        CampaignType = campaignType,
        EarningType = EarningType.CardBased,
        StartDate = DateTime.Now.AddDays(-10),
        EndDate = DateTime.Now.AddDays(20),
        RewardPoint = 25m,
        Status = CampaignStatus.Ongoing,
        IsActive = true
    };

    private static Customer NewCustomer() => new()
    {
        CustomerNumber = $"T{DateTime.Now.Ticks.ToString()[^8..]}{Interlocked.Increment(ref _sequence):D3}",
        Gender = Gender.Female,
        SegmentId = FarmerSegmentId,
        IsActive = true
    };

    private static Card NewCard(Customer customer) => new()
    {
        Customer = customer,
        ProductId = VisaGoldProductId,
        CardType = CardType.Primary,
        IsActive = true
    };
}
