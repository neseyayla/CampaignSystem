using CampaignSystem.Configuration;
using CampaignSystem.Data;
using CampaignSystem.Entities;
using CampaignSystem.Enums;
using CampaignSystem.Repositories;
using CampaignSystem.Services;
using CampaignSystem.Services.Caching;
using CampaignSystem.Tests.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CampaignSystem.Tests;

/// <summary>
/// What a customer is allowed to see and join.
///
/// Eligibility is the one rule the customer facing service owns, and it is easy to get
/// subtly wrong in a way no screen would reveal: a customer who is shown a campaign they
/// can never earn from is only disappointed later, and one who is hidden a campaign they
/// qualify for never finds out at all. These tests pin the boundary in both directions.
///
/// Every test runs inside a transaction that is rolled back, so the tests leave nothing
/// behind and their order does not matter. Assertions ask whether a particular campaign is
/// present rather than counting the list, so a test running alongside them cannot upset it.
/// </summary>
public class CustomerCampaignServiceTests(TestDatabaseFixture fixture) : IClassFixture<TestDatabaseFixture>
{
    private const int StudentSegmentId = 1;
    private const int FarmerSegmentId = 3;
    private const int RetiredSegmentId = 5;

    private const int VisaClassicProductId = 1;
    private const int VisaGoldProductId = 3;
    private const int PlatinumProductId = 5;

    private const int OpetMerchantId = 3;

    private static int _sequence;

    // A fresh catalog cache per service so the tests stay isolated. The cache holds only the
    // person-independent catalog; the per-customer eligibility and enrolment are recomputed on
    // every call, which is what lets an enrolment made through the service show up on the very
    // next read even though the catalog is cached.
    private static CustomerCampaignService CreateService(CampaignDbContext context) =>
        new(context,
            new ParticipationService(
                new Repository<CampaignParticipation>(context),
                new Repository<Campaign>(context),
                new Repository<Customer>(context),
                new Repository<Card>(context)),
            new RewardService(
                context, new RewardCalculator(context), Options.Create(new RewardCalculationOptions()), NullLogger<RewardService>.Instance),
            new CampaignCatalogCache(new MemoryCache(new MemoryCacheOptions())));

    [Fact]
    public async Task UnknownCustomerIsReported_RatherThanReturningAnEmptyList()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        // Null and empty mean different things to the caller: one is a 404, the other is a
        // customer who simply has nothing on offer.
        Assert.Null(await CreateService(context).GetEligibleAsync(-1));
    }

    [Fact]
    public async Task RunningUpcomingAndLoadingAreListed_ButNotEnded()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var ongoing = NewCampaign(CampaignStatus.Ongoing);
        var pending = NewCampaign(CampaignStatus.Pending);
        var loading = NewCampaign(CampaignStatus.Loading);
        var ended = NewCampaign(CampaignStatus.Ended);

        var customer = NewCustomer();
        context.AddRange(ongoing, pending, loading, ended, customer, NewCard(customer, VisaGoldProductId));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetEligibleAsync(customer.Id);

        Assert.True(Lists(result, ongoing));

        // Upcoming campaigns are listed on purpose — an enrollment campaign is worth
        // joining before it starts, which is the only time joining early is possible.
        Assert.True(Lists(result, pending));
        Assert.False(result!.Single(c => c.CampaignId == pending.Id).HasStarted);

        // A Loading campaign — ended, but its rewards not loaded yet — is kept and flagged
        // reward-pending, so a customer who took part does not see it vanish for the days
        // until the batch pays it. An Ended one is gone from here; its points are on the
        // rewards endpoint by then.
        Assert.True(Lists(result, loading));
        Assert.True(result!.Single(c => c.CampaignId == loading.Id).RewardPending);
        Assert.False(result.Single(c => c.CampaignId == ongoing.Id).RewardPending);
        Assert.False(Lists(result, ended));
    }

    [Fact]
    public async Task SegmentCriterionHidesTheCampaignFromEveryOtherSegment()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var campaign = NewCampaign();
        var farmer = NewCustomer(FarmerSegmentId);
        var retiree = NewCustomer(RetiredSegmentId);

        context.AddRange(campaign, farmer, retiree);
        await context.SaveChangesAsync();

        context.AddRange(
            NewCard(farmer, VisaGoldProductId),
            NewCard(retiree, VisaGoldProductId),
            new CampaignSegment { CampaignId = campaign.Id, SegmentId = FarmerSegmentId });

        await context.SaveChangesAsync();

        var service = CreateService(context);

        Assert.True(Lists(await service.GetEligibleAsync(farmer.Id), campaign));
        Assert.False(Lists(await service.GetEligibleAsync(retiree.Id), campaign));
    }

    [Fact]
    public async Task GenderCriterionHidesTheCampaignFromTheOtherGender()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var campaign = NewCampaign(gender: Gender.Female);
        var woman = NewCustomer(gender: Gender.Female);
        var man = NewCustomer(gender: Gender.Male);

        context.AddRange(campaign, woman, man);
        await context.SaveChangesAsync();

        context.AddRange(NewCard(woman, VisaGoldProductId), NewCard(man, VisaGoldProductId));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        Assert.True(Lists(await service.GetEligibleAsync(woman.Id), campaign));
        Assert.False(Lists(await service.GetEligibleAsync(man.Id), campaign));
    }

    [Fact]
    public async Task ProductCriterionNeedsTheCustomerToHoldOneOfThoseCards()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var campaign = NewCampaign();
        var platinumHolder = NewCustomer();
        var classicHolder = NewCustomer();

        context.AddRange(campaign, platinumHolder, classicHolder);
        await context.SaveChangesAsync();

        context.AddRange(
            NewCard(platinumHolder, PlatinumProductId),
            NewCard(classicHolder, VisaClassicProductId),
            new CampaignProduct { CampaignId = campaign.Id, ProductId = PlatinumProductId });

        await context.SaveChangesAsync();

        var service = CreateService(context);

        Assert.True(Lists(await service.GetEligibleAsync(platinumHolder.Id), campaign));
        Assert.False(Lists(await service.GetEligibleAsync(classicHolder.Id), campaign));
    }

    [Fact]
    public async Task ProductAndCardTypeMustBeMetByOneAndTheSameCard()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        // A Platinum campaign for supplementary cards only.
        var campaign = NewCampaign(cardType: CardType.Supplementary);

        // Holds a Platinum card and holds a supplementary card, but not one card that is
        // both. No transaction of theirs could ever qualify, so the campaign must not be
        // offered — checking the two criteria separately would wrongly let them through.
        var splitHolder = NewCustomer();

        // The control: one card meeting both conditions at once.
        var properHolder = NewCustomer();

        context.AddRange(campaign, splitHolder, properHolder);
        await context.SaveChangesAsync();

        context.AddRange(
            NewCard(splitHolder, PlatinumProductId, CardType.Primary),
            NewCard(splitHolder, VisaClassicProductId, CardType.Supplementary),
            NewCard(properHolder, PlatinumProductId, CardType.Supplementary),
            new CampaignProduct { CampaignId = campaign.Id, ProductId = PlatinumProductId });

        await context.SaveChangesAsync();

        var service = CreateService(context);

        Assert.False(Lists(await service.GetEligibleAsync(splitHolder.Id), campaign));
        Assert.True(Lists(await service.GetEligibleAsync(properHolder.Id), campaign));
    }

    [Fact]
    public async Task ClosedCardsDoNotMakeTheirHolderEligible()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var campaign = NewCampaign();
        var customer = NewCustomer();

        context.AddRange(campaign, customer);
        await context.SaveChangesAsync();

        // The right product, but the card is closed and can no longer be spent on.
        context.AddRange(
            NewCard(customer, PlatinumProductId, isActive: false),
            NewCard(customer, VisaClassicProductId),
            new CampaignProduct { CampaignId = campaign.Id, ProductId = PlatinumProductId });

        await context.SaveChangesAsync();

        Assert.False(Lists(await CreateService(context).GetEligibleAsync(customer.Id), campaign));
    }

    [Fact]
    public async Task MerchantCriterionExcludesNobodyAndIsShownAsATerm()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var campaign = NewCampaign();
        var customer = NewCustomer();

        context.AddRange(campaign, customer);
        await context.SaveChangesAsync();

        context.AddRange(
            NewCard(customer, VisaGoldProductId),
            new CampaignMerchant { CampaignId = campaign.Id, MerchantId = OpetMerchantId });

        await context.SaveChangesAsync();

        var result = await CreateService(context).GetEligibleAsync(customer.Id);

        // Where the money is spent says nothing about who the customer is, so the campaign
        // stays on offer — it simply comes with a condition attached, named rather than
        // numbered so the screen can print it.
        var listed = result!.Single(c => c.CampaignId == campaign.Id);

        Assert.Equal(["Opet"], listed.Merchants);
    }

    [Fact]
    public async Task ACustomerWithNoCardIsOfferedNothing()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var campaign = NewCampaign();
        var customer = NewCustomer();

        context.AddRange(campaign, customer);
        await context.SaveChangesAsync();

        // No card at all: nothing to spend on, so no campaign can pay them.
        Assert.False(Lists(await CreateService(context).GetEligibleAsync(customer.Id), campaign));
    }

    [Fact]
    public async Task AnIneligibleCampaignIsNotFound_RatherThanRefused()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var campaign = NewCampaign();
        var retiree = NewCustomer(RetiredSegmentId);

        context.AddRange(campaign, retiree);
        await context.SaveChangesAsync();

        context.AddRange(
            NewCard(retiree, VisaGoldProductId),
            new CampaignSegment { CampaignId = campaign.Id, SegmentId = StudentSegmentId });

        await context.SaveChangesAsync();

        // Not "you may not have this one", which would confirm the campaign exists and is
        // running for somebody else. As far as this customer is concerned there is nothing
        // at that address.
        var result = await CreateService(context).GetOneAsync(retiree.Id, campaign.Id);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task EnrollingInACampaignTheCustomerCannotEarnFromIsRefused()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var campaign = NewCampaign(campaignType: CampaignType.EnrollmentRequired);
        var retiree = NewCustomer(RetiredSegmentId);

        context.AddRange(campaign, retiree);
        await context.SaveChangesAsync();

        var card = NewCard(retiree, VisaGoldProductId);

        context.AddRange(card, new CampaignSegment { CampaignId = campaign.Id, SegmentId = StudentSegmentId });
        await context.SaveChangesAsync();

        // The administrative endpoint would allow this — a branch may have a reason to
        // enroll someone the criteria do not reach. A request from the customer's own
        // screen has no such licence.
        var result = await CreateService(context).EnrollAsync(retiree.Id, campaign.Id, card.Id);

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Empty(context.CampaignParticipations.Where(p => p.CampaignId == campaign.Id));
    }

    [Fact]
    public async Task EnrollingMarksTheCampaignAsJoined()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var campaign = NewCampaign(campaignType: CampaignType.EnrollmentRequired);
        var customer = NewCustomer();

        context.AddRange(campaign, customer);
        await context.SaveChangesAsync();

        var card = NewCard(customer, VisaGoldProductId);
        context.Add(card);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Before enrolling the campaign is on offer but not joined.
        var before = (await service.GetEligibleAsync(customer.Id))!
            .Single(c => c.CampaignId == campaign.Id);

        Assert.True(before.EnrollmentRequired);
        Assert.False(before.Enrolled);

        var result = await service.EnrollAsync(customer.Id, campaign.Id, card.Id);
        Assert.Equal(ResultStatus.Success, result.Status);

        var after = await service.GetEligibleAsync(customer.Id);
        Assert.True(after!.Single(c => c.CampaignId == campaign.Id).Enrolled);

        // Joining twice is the same request arriving twice, not a second enrollment.
        var again = await service.EnrollAsync(customer.Id, campaign.Id, card.Id);
        Assert.Equal(ResultStatus.Conflict, again.Status);
    }

    [Fact]
    public async Task ACancelledEnrollmentDoesNotCountAsJoined()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var campaign = NewCampaign(campaignType: CampaignType.EnrollmentRequired);
        var customer = NewCustomer();

        context.AddRange(campaign, customer);
        await context.SaveChangesAsync();

        var card = NewCard(customer, VisaGoldProductId);
        context.Add(card);
        await context.SaveChangesAsync();

        context.Add(new CampaignParticipation
        {
            CampaignId = campaign.Id,
            CustomerId = customer.Id,
            CardId = card.Id,
            ParticipationDate = DateTime.Now,
            Status = ParticipationStatus.Cancelled
        });

        await context.SaveChangesAsync();

        var result = await CreateService(context).GetEligibleAsync(customer.Id);

        // A cancelled enrollment earns nothing, so showing it as joined would leave the
        // customer expecting a reward that will never arrive.
        Assert.False(result!.Single(c => c.CampaignId == campaign.Id).Enrolled);
    }

    [Fact]
    public async Task TheDetailViewCarriesTheCustomersOwnStanding()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var campaign = NewCampaign();
        campaign.MinimumAmount = 250m;
        campaign.RewardPoint = 50m;

        var customer = NewCustomer();

        context.AddRange(campaign, customer);
        await context.SaveChangesAsync();

        var card = NewCard(customer, VisaGoldProductId);
        context.Add(card);
        await context.SaveChangesAsync();

        var suffix = DateTime.Now.Ticks.ToString()[^10..];

        // Two qualifying sales and one below the minimum.
        context.AddRange(
            NewTransaction(customer, card, 300m, campaign.StartDate.AddDays(1), $"P{suffix}1"),
            NewTransaction(customer, card, 800m, campaign.StartDate.AddDays(2), $"P{suffix}2"),
            NewTransaction(customer, card, 100m, campaign.StartDate.AddDays(3), $"P{suffix}3"));

        await context.SaveChangesAsync();

        var result = await CreateService(context).GetOneAsync(customer.Id, campaign.Id);

        Assert.Equal(ResultStatus.Success, result.Status);
        Assert.Equal(campaign.Id, result.Value!.Campaign.CampaignId);

        // The same query the batch will run at the end, so the figure the customer watches
        // and the figure they are eventually paid cannot drift apart.
        var line = Assert.Single(result.Value.Progress.Lines);

        Assert.Equal(card.Id, line.CardId);
        Assert.Equal(2, line.QualifyingCount);
        Assert.Equal(100m, result.Value.Progress.TotalRewardPoint);
    }

    private static bool Lists(List<DTOs.CustomerCampaignDto>? result, Campaign campaign)
        => result!.Any(c => c.CampaignId == campaign.Id);

    /// <summary>
    /// A campaign whose dates match the status it is given, so a Pending one really is in
    /// the future and an Ended one really is behind us.
    /// </summary>
    private static Campaign NewCampaign(
        CampaignStatus status = CampaignStatus.Ongoing,
        CampaignType campaignType = CampaignType.Mass,
        EarningType earningType = EarningType.CardBased,
        Gender? gender = null,
        CardType? cardType = null)
    {
        var (start, end) = status switch
        {
            CampaignStatus.Pending => (DateTime.Now.AddDays(5), DateTime.Now.AddDays(35)),
            CampaignStatus.Ongoing => (DateTime.Now.AddDays(-10), DateTime.Now.AddDays(20)),
            _ => (DateTime.Now.AddDays(-60), DateTime.Now.AddDays(-30))
        };

        return new Campaign
        {
            Name = "Customer view test campaign",
            CampaignType = campaignType,
            EarningType = earningType,
            Gender = gender,
            CardType = cardType,
            StartDate = start,
            EndDate = end,
            RewardPoint = 25m,
            Status = status,
            IsActive = true
        };
    }

    private static Customer NewCustomer(
        int? segmentId = FarmerSegmentId,
        Gender gender = Gender.Female) => new()
    {
        // Unique per call rather than per test: several customers are created within a
        // single test and the customer number is unique in the database.
        CustomerNumber = $"T{DateTime.Now.Ticks.ToString()[^8..]}{Interlocked.Increment(ref _sequence):D3}",
        Gender = gender,
        SegmentId = segmentId,
        IsActive = true
    };

    /// <summary>
    /// The owner is set through the navigation property rather than the key, so a card can
    /// be created before its customer has been saved and still resolve to the right row.
    /// </summary>
    private static Card NewCard(
        Customer customer,
        int productId,
        CardType cardType = CardType.Primary,
        bool isActive = true) => new()
    {
        Customer = customer,
        ProductId = productId,
        CardType = cardType,
        IsActive = isActive
    };

    private static Transaction NewTransaction(
        Customer customer,
        Card card,
        decimal amount,
        DateTime date,
        string rrn) => new()
    {
        Rrn = rrn,
        CardId = card.Id,
        CustomerId = customer.Id,
        MerchantId = OpetMerchantId,
        TransactionCodeId = 1,
        TransactionDate = date,
        Amount = amount
    };
}
