using CampaignSystem.Configuration;
using CampaignSystem.Data;
using CampaignSystem.Entities;
using CampaignSystem.Enums;
using CampaignSystem.Services;
using CampaignSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CampaignSystem.Tests;

/// <summary>
/// The figures asserted here are the ones that were verified by hand against the database
/// before the batch was trusted. Written down as tests, they stop a later change from
/// quietly paying customers the wrong amount.
///
/// Every test runs inside a transaction that is rolled back, so the tests leave nothing
/// behind and their order does not matter.
/// </summary>
public class RewardServiceTests(TestDatabaseFixture fixture) : IClassFixture<TestDatabaseFixture>
{
    private static RewardService CreateService(CampaignDbContext context, int daysAfterCampaignEnd = 0) =>
        new(context, new RewardCalculator(context), Options.Create(new RewardCalculationOptions
        {
            DaysAfterCampaignEnd = daysAfterCampaignEnd
        }), NullLogger<RewardService>.Instance);

    [Fact]
    public async Task CardBasedCampaign_WritesOneRewardPerCard()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var campaign = ScenarioBuilder.FinishedCampaign(EarningType.CardBased);
        var scenario = await ScenarioBuilder.CreateAsync(context, campaign);

        var result = await CreateService(context).CalculateAsync(campaign.Id);

        Assert.Equal(ResultStatus.Success, result.Status);
        Assert.Equal(5, result.Value!.QualifyingTransactions);
        Assert.Equal(2, result.Value.RewardsCreated);
        Assert.Equal(250m, result.Value.TotalRewardPoint);

        var rewards = await context.CampaignRewards
            .Where(r => r.CampaignId == campaign.Id)
            .ToListAsync();

        // Three transactions on card A at 50 a piece, two on card B.
        Assert.Equal(150m, rewards.Single(r => r.CardId == scenario.CardA.Id).RewardPoint);
        Assert.Equal(100m, rewards.Single(r => r.CardId == scenario.CardB.Id).RewardPoint);

        // A card based reward always names its card.
        Assert.All(rewards, reward => Assert.NotNull(reward.CardId));
    }

    [Fact]
    public async Task CustomerBasedCampaign_PoolsEveryCardIntoOneReward()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var campaign = ScenarioBuilder.FinishedCampaign(EarningType.CustomerBased);
        await ScenarioBuilder.CreateAsync(context, campaign);

        var result = await CreateService(context).CalculateAsync(campaign.Id);

        Assert.Equal(ResultStatus.Success, result.Status);
        Assert.Equal(5, result.Value!.QualifyingTransactions);
        Assert.Equal(1, result.Value.RewardsCreated);
        Assert.Equal(250m, result.Value.TotalRewardPoint);

        var reward = await context.CampaignRewards.SingleAsync(r => r.CampaignId == campaign.Id);

        // The null card is the point: the reward belongs to the customer, not to one card.
        Assert.Null(reward.CardId);
        Assert.Equal(5, reward.QualifyingCount);
    }

    [Fact]
    public async Task MaxRewardAmount_CapsEachRewardRow()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        // Card A would earn 150 and card B 100; a cap of 120 bites on A only.
        var campaign = ScenarioBuilder.FinishedCampaign(EarningType.CardBased, maxRewardAmount: 120m);
        var scenario = await ScenarioBuilder.CreateAsync(context, campaign);

        var result = await CreateService(context).CalculateAsync(campaign.Id);

        Assert.Equal(220m, result.Value!.TotalRewardPoint);

        var rewards = await context.CampaignRewards
            .Where(r => r.CampaignId == campaign.Id)
            .ToListAsync();

        Assert.Equal(120m, rewards.Single(r => r.CardId == scenario.CardA.Id).RewardPoint);
        Assert.Equal(100m, rewards.Single(r => r.CardId == scenario.CardB.Id).RewardPoint);
    }

    [Fact]
    public async Task TransactionsOutsideTheCampaignPeriod_AreIgnored()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var campaign = ScenarioBuilder.FinishedCampaign(EarningType.CustomerBased);
        await ScenarioBuilder.CreateAsync(context, campaign);

        var result = await CreateService(context).CalculateAsync(campaign.Id);

        // Seven transactions exist; one is under the minimum and one predates the campaign.
        Assert.Equal(5, result.Value!.QualifyingTransactions);
    }

    [Fact]
    public async Task MerchantCriterion_ExcludesOtherMerchants()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var campaign = ScenarioBuilder.FinishedCampaign(EarningType.CustomerBased);
        await ScenarioBuilder.CreateAsync(context, campaign);

        // Every transaction in the scenario is at Opet, so restricting to a different
        // merchant must leave nothing behind.
        context.CampaignMerchants.Add(new CampaignMerchant
        {
            CampaignId = campaign.Id,
            MerchantId = 1
        });

        await context.SaveChangesAsync();

        var result = await CreateService(context).CalculateAsync(campaign.Id);

        Assert.Equal(0, result.Value!.QualifyingTransactions);
        Assert.Equal(0, result.Value.RewardsCreated);
    }

    [Fact]
    public async Task EmptyCriteria_PlaceNoRestriction()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        // No criteria rows at all, and no amount bounds: every transaction in the period
        // counts, including the 100 that a minimum would have excluded.
        var campaign = ScenarioBuilder.FinishedCampaign(EarningType.CustomerBased, minimumAmount: null);
        await ScenarioBuilder.CreateAsync(context, campaign);

        var result = await CreateService(context).CalculateAsync(campaign.Id);

        Assert.Equal(6, result.Value!.QualifyingTransactions);
    }

    [Fact]
    public async Task CalculatingTwice_IsRefused()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var campaign = ScenarioBuilder.FinishedCampaign(EarningType.CardBased);
        await ScenarioBuilder.CreateAsync(context, campaign);

        var service = CreateService(context);

        var first = await service.CalculateAsync(campaign.Id);
        var second = await service.CalculateAsync(campaign.Id);

        Assert.Equal(ResultStatus.Success, first.Status);

        // Rewards are money: a second run must refuse rather than pay again.
        Assert.Equal(ResultStatus.Invalid, second.Status);
        Assert.Equal(CampaignStatus.Ended, campaign.Status);
    }

    [Fact]
    public async Task OngoingCampaign_CannotBeCalculated()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var campaign = ScenarioBuilder.FinishedCampaign(EarningType.CardBased);
        campaign.EndDate = DateTime.Now.AddDays(10);
        campaign.Status = CampaignStatus.Ongoing;

        await ScenarioBuilder.CreateAsync(context, campaign);

        var result = await CreateService(context).CalculateAsync(campaign.Id);

        // Paying out mid-campaign would reward transactions that have not finished arriving.
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Empty(await context.CampaignRewards.Where(r => r.CampaignId == campaign.Id).ToListAsync());
    }

    [Fact]
    public async Task RewardsAreNotLoadedBeforeTheConfiguredDay()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        // The campaign ended 30 days ago, so a 60 day wait has not elapsed.
        var campaign = ScenarioBuilder.FinishedCampaign(EarningType.CardBased);
        await ScenarioBuilder.CreateAsync(context, campaign);

        var result = await CreateService(context, daysAfterCampaignEnd: 60).CalculateAsync(campaign.Id);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal(CampaignStatus.Loading, campaign.Status);
    }

    [Fact]
    public async Task RewardsAreLoadedOnTheDayTheyFallDue()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        // A campaign runs to the last second of its closing day and the batch runs in the
        // small hours. Counting the wait in hours rather than days would leave a campaign
        // that came due today looking a few hours short, and pay it a day late.
        var campaign = ScenarioBuilder.FinishedCampaign(EarningType.CardBased);
        campaign.EndDate = DateTime.Now.Date.AddDays(-5).AddHours(23).AddMinutes(59).AddSeconds(59);
        campaign.StartDate = campaign.EndDate.AddDays(-30);

        await ScenarioBuilder.CreateAsync(context, campaign);

        var result = await CreateService(context, daysAfterCampaignEnd: 5).CalculateAsync(campaign.Id);

        Assert.Equal(ResultStatus.Success, result.Status);
    }

    [Fact]
    public async Task RewardsAreNotLoadedTheDayBeforeTheyFallDue()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        // One day short of the wait: the campaign ended four days ago and the business asked
        // for five.
        var campaign = ScenarioBuilder.FinishedCampaign(EarningType.CardBased);
        campaign.EndDate = DateTime.Now.Date.AddDays(-4).AddHours(23).AddMinutes(59).AddSeconds(59);
        campaign.StartDate = campaign.EndDate.AddDays(-30);

        await ScenarioBuilder.CreateAsync(context, campaign);

        var result = await CreateService(context, daysAfterCampaignEnd: 5).CalculateAsync(campaign.Id);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal(CampaignStatus.Loading, campaign.Status);
    }

    [Fact]
    public async Task Preview_MatchesWhatTheBatchWouldPay()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var campaign = ScenarioBuilder.FinishedCampaign(EarningType.CardBased, maxRewardAmount: 120m);
        var scenario = await ScenarioBuilder.CreateAsync(context, campaign);

        var service = CreateService(context);

        var preview = await service.PreviewAsync(campaign.Id, scenario.Customer.Id);
        var calculation = await service.CalculateAsync(campaign.Id);

        // The whole reason preview and the batch share one query: what the customer is shown
        // during the campaign has to be what they are eventually paid.
        Assert.Equal(calculation.Value!.TotalRewardPoint, preview.Value!.TotalRewardPoint);

        var cappedLine = preview.Value.Lines.Single(l => l.CardId == scenario.CardA.Id);

        Assert.True(cappedLine.CapApplied);
        Assert.Equal(150m, cappedLine.EarnedBeforeCap);
        Assert.Equal(120m, cappedLine.RewardPoint);
    }

    [Fact]
    public async Task GenderFilter_ExcludesCustomersOfAnotherGender()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        // The scenario's customer is female; restricting the campaign to men must leave
        // nothing behind.
        var campaign = ScenarioBuilder.FinishedCampaign(EarningType.CardBased);
        campaign.Gender = Gender.Male;

        await ScenarioBuilder.CreateAsync(context, campaign);

        var result = await CreateService(context).CalculateAsync(campaign.Id);

        Assert.Equal(0, result.Value!.QualifyingTransactions);
        Assert.Equal(0, result.Value.RewardsCreated);
    }

    [Fact]
    public async Task GenderFilter_KeepsCustomersOfTheSameGender()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var campaign = ScenarioBuilder.FinishedCampaign(EarningType.CardBased);
        campaign.Gender = Gender.Female;

        await ScenarioBuilder.CreateAsync(context, campaign);

        var result = await CreateService(context).CalculateAsync(campaign.Id);

        // Same five transactions as with no filter at all: naming the customer's own gender
        // narrows nothing.
        Assert.Equal(5, result.Value!.QualifyingTransactions);
        Assert.Equal(250m, result.Value.TotalRewardPoint);
    }

    [Fact]
    public async Task CardTypeFilter_KeepsOnlyCardsOfThatType()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        // Card A is primary and card B supplementary, so restricting to primary cards drops
        // card B's two transactions and its whole reward row with them.
        var campaign = ScenarioBuilder.FinishedCampaign(EarningType.CardBased);
        campaign.CardType = CardType.Primary;

        var scenario = await ScenarioBuilder.CreateAsync(context, campaign);

        var result = await CreateService(context).CalculateAsync(campaign.Id);

        Assert.Equal(3, result.Value!.QualifyingTransactions);
        Assert.Equal(1, result.Value.RewardsCreated);
        Assert.Equal(150m, result.Value.TotalRewardPoint);

        var reward = await context.CampaignRewards.SingleAsync(r => r.CampaignId == campaign.Id);

        Assert.Equal(scenario.CardA.Id, reward.CardId);
    }

    [Fact]
    public async Task DemographicFiltersCombine_RatherThanReplacingEachOther()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        // Right gender, wrong card type: matching one condition is not enough.
        var campaign = ScenarioBuilder.FinishedCampaign(EarningType.CardBased);
        campaign.Gender = Gender.Female;
        campaign.CardType = CardType.Supplementary;

        var scenario = await ScenarioBuilder.CreateAsync(context, campaign);

        var result = await CreateService(context).CalculateAsync(campaign.Id);

        Assert.Equal(2, result.Value!.QualifyingTransactions);
        Assert.Equal(100m, result.Value.TotalRewardPoint);

        var reward = await context.CampaignRewards.SingleAsync(r => r.CampaignId == campaign.Id);

        Assert.Equal(scenario.CardB.Id, reward.CardId);
    }

    [Fact]
    public async Task Preview_DoesNotCountTransactionsDatedInTheFuture()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var suffix = DateTime.Now.Ticks.ToString()[^10..];

        // A campaign still running: its window reaches into the future, so a transaction can be
        // dated after now yet inside the period. The preview must not pay on it.
        var campaign = new Campaign
        {
            Name = "Future test",
            CampaignType = CampaignType.Mass,
            EarningType = EarningType.CustomerBased,
            StartDate = DateTime.Now.AddDays(-10),
            EndDate = DateTime.Now.AddDays(20),
            MinimumAmount = 250m,
            RewardPoint = 50m,
            Status = CampaignStatus.Ongoing,
            IsActive = true
        };

        var (customer, card) = await AddCustomerWithCardAsync(context, campaign, suffix);

        context.Transactions.AddRange(
            NewTransaction(suffix, 0, card, customer, 300m, DateTime.Now.AddDays(-2)),   // already happened
            NewTransaction(suffix, 1, card, customer, 300m, DateTime.Now.AddDays(5)));   // not yet
        await context.SaveChangesAsync();

        var preview = await CreateService(context).PreviewAsync(campaign.Id, customer.Id);

        Assert.Equal(ResultStatus.Success, preview.Status);

        // Only the past transaction pays; the future one is not counted.
        Assert.Equal(50m, preview.Value!.TotalRewardPoint);
        Assert.Equal(1, preview.Value.Lines.Single().QualifyingCount);
    }

    [Fact]
    public async Task EnrollmentCampaign_CountsOnlyFromTheJoinDate()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var suffix = DateTime.Now.Ticks.ToString()[^10..];

        var campaign = new Campaign
        {
            Name = "Enrollment test",
            CampaignType = CampaignType.EnrollmentRequired,
            EarningType = EarningType.CardBased,
            StartDate = DateTime.Now.AddDays(-60),
            EndDate = DateTime.Now.AddDays(-30),
            MinimumAmount = 250m,
            RewardPoint = 50m,
            Status = CampaignStatus.Loading,
            IsActive = true
        };

        var (customer, card) = await AddCustomerWithCardAsync(context, campaign, suffix);

        // Joined halfway through the campaign, at the card level.
        var joinDate = campaign.StartDate.AddDays(15);
        context.CampaignParticipations.Add(new CampaignParticipation
        {
            CampaignId = campaign.Id,
            CustomerId = customer.Id,
            CardId = card.Id,
            ParticipationDate = joinDate,
            Status = ParticipationStatus.Active
        });

        context.Transactions.AddRange(
            NewTransaction(suffix, 0, card, customer, 300m, joinDate.AddDays(-5)),  // before joining
            NewTransaction(suffix, 1, card, customer, 300m, joinDate.AddDays(5)));  // after joining
        await context.SaveChangesAsync();

        var result = await CreateService(context).CalculateAsync(campaign.Id);

        Assert.Equal(ResultStatus.Success, result.Status);

        // Only the transaction made after the join date counts; the earlier one is ignored.
        Assert.Equal(1, result.Value!.QualifyingTransactions);
        Assert.Equal(50m, result.Value.TotalRewardPoint);
    }

    [Fact]
    public async Task ReversedTransaction_IsNotCountedWhenTheRewardIsCalculated()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var suffix = DateTime.Now.Ticks.ToString()[^10..];

        var campaign = new Campaign
        {
            Name = "Reversal calc test",
            CampaignType = CampaignType.Mass,
            EarningType = EarningType.CardBased,
            StartDate = DateTime.Now.AddDays(-10),
            EndDate = DateTime.Now.AddDays(-1),
            MinimumAmount = 250m,
            RewardPoint = 50m,
            Status = CampaignStatus.Loading,
            IsActive = true
        };

        var (customer, card) = await AddCustomerWithCardAsync(context, campaign, suffix);

        var kept = NewTransaction(suffix, 0, card, customer, 300m, campaign.StartDate.AddDays(1));
        var refunded = NewTransaction(suffix, 1, card, customer, 300m, campaign.StartDate.AddDays(2));
        context.Transactions.AddRange(kept, refunded);
        await context.SaveChangesAsync();

        // A refund row pointing at the purchase is what stops it counting — there is no flag.
        context.Transactions.Add(NewRefund(suffix, 2, refunded));
        await context.SaveChangesAsync();

        var result = await CreateService(context).CalculateAsync(campaign.Id);

        Assert.Equal(ResultStatus.Success, result.Status);

        // Only the purchase without a refund pays.
        Assert.Equal(1, result.Value!.QualifyingTransactions);
        Assert.Equal(50m, result.Value.TotalRewardPoint);
    }

    [Fact]
    public async Task ReconcileReversals_ReducesARewardAfterOneOfItsTransactionsIsRefunded()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var suffix = DateTime.Now.Ticks.ToString()[^10..];

        var campaign = new Campaign
        {
            Name = "Reconcile test",
            CampaignType = CampaignType.Mass,
            EarningType = EarningType.CardBased,
            StartDate = DateTime.Now.AddDays(-10),
            EndDate = DateTime.Now.AddDays(-1),
            MinimumAmount = 250m,
            RewardPoint = 50m,
            RefundClawbackEnabled = true,
            Status = CampaignStatus.Loading,
            IsActive = true
        };

        var (customer, card) = await AddCustomerWithCardAsync(context, campaign, suffix);

        var first = NewTransaction(suffix, 0, card, customer, 300m, campaign.StartDate.AddDays(1));
        var second = NewTransaction(suffix, 1, card, customer, 300m, campaign.StartDate.AddDays(2));
        context.Transactions.AddRange(first, second);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Both transactions count: the reward is loaded at 100, dated now (inside the window).
        var calculation = await service.CalculateAsync(campaign.Id);
        Assert.Equal(100m, calculation.Value!.TotalRewardPoint);

        // One of the two purchases is refunded after the reward was loaded: a refund row lands.
        context.Transactions.Add(NewRefund(suffix, 2, second));
        await context.SaveChangesAsync();

        var clawbacks = await service.ReconcileReversalsAsync();

        Assert.Equal(1, clawbacks);

        // The Earn row is untouched; a negative Clawback row records the loss, so the two net to 50.
        var rows = await context.CampaignRewards
            .Where(r => r.CampaignId == campaign.Id)
            .ToListAsync();

        Assert.Equal(50m, rows.Sum(r => r.RewardPoint));
        Assert.Equal(100m, rows.Single(r => r.RewardType == RewardType.Earn).RewardPoint);

        var clawback = rows.Single(r => r.RewardType == RewardType.Clawback);
        Assert.Equal(-50m, clawback.RewardPoint);
        Assert.Equal(-1, clawback.QualifyingCount);

        // A second run finds nothing more to do — reconciliation is idempotent.
        Assert.Equal(0, await service.ReconcileReversalsAsync());
    }

    [Fact]
    public async Task ReconcileReversals_SkipsACampaignThatDoesNotReclaimPoints()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (campaign, service) = await PaidCampaignWithOneRefundAsync(
            context, clawbackEnabled: false, clawbackDays: null);

        // Clawback is off, so the refund is ignored: no Clawback row, points unchanged.
        Assert.Equal(0, await service.ReconcileReversalsAsync());

        var rows = await context.CampaignRewards.Where(r => r.CampaignId == campaign.Id).ToListAsync();
        Assert.DoesNotContain(rows, r => r.RewardType == RewardType.Clawback);
        Assert.Equal(100m, rows.Sum(r => r.RewardPoint));
    }

    [Fact]
    public async Task ReconcileReversals_SkipsACampaignPastItsRefundWindow()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (campaign, service) = await PaidCampaignWithOneRefundAsync(
            context, clawbackEnabled: true, clawbackDays: 10);

        // The reward was loaded 20 days ago — past the 10-day window, so nothing is clawed back.
        await ShiftRewardLoadDateAsync(context, campaign.Id, DateTime.Now.AddDays(-20));

        Assert.Equal(0, await service.ReconcileReversalsAsync());
        Assert.DoesNotContain(
            await context.CampaignRewards.Where(r => r.CampaignId == campaign.Id).ToListAsync(),
            r => r.RewardType == RewardType.Clawback);
    }

    [Fact]
    public async Task ReconcileReversals_StillClawsBackOnTheWindowsLastDay()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (campaign, service) = await PaidCampaignWithOneRefundAsync(
            context, clawbackEnabled: true, clawbackDays: 30);

        // Loaded exactly 30 days ago: today is the last day of the window, so it still claws back.
        await ShiftRewardLoadDateAsync(context, campaign.Id, DateTime.Now.AddDays(-30));

        Assert.Equal(1, await service.ReconcileReversalsAsync());
    }

    [Fact]
    public async Task PartialRefund_StillCounts_WhenTheRemainderMeetsTheMinimum()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var suffix = DateTime.Now.Ticks.ToString()[^10..];

        var campaign = new Campaign
        {
            Name = "Partial above min",
            CampaignType = CampaignType.Mass,
            EarningType = EarningType.CardBased,
            StartDate = DateTime.Now.AddDays(-10),
            EndDate = DateTime.Now.AddDays(-1),
            MinimumAmount = 250m,
            RewardPoint = 50m,
            Status = CampaignStatus.Loading,
            IsActive = true
        };

        var (customer, card) = await AddCustomerWithCardAsync(context, campaign, suffix);

        var purchase = NewTransaction(suffix, 0, card, customer, 300m, campaign.StartDate.AddDays(1));
        context.Transactions.Add(purchase);
        await context.SaveChangesAsync();

        // 300 − 30 = 270, still at or above the 250 minimum.
        context.Transactions.Add(NewPartialRefund(suffix, 1, purchase, 30m));
        await context.SaveChangesAsync();

        var result = await CreateService(context).CalculateAsync(campaign.Id);

        Assert.Equal(1, result.Value!.QualifyingTransactions);
        Assert.Equal(50m, result.Value.TotalRewardPoint);
    }

    [Fact]
    public async Task PartialRefund_StopsCounting_WhenTheRemainderFallsBelowTheMinimum()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var suffix = DateTime.Now.Ticks.ToString()[^10..];

        var campaign = new Campaign
        {
            Name = "Partial below min",
            CampaignType = CampaignType.Mass,
            EarningType = EarningType.CardBased,
            StartDate = DateTime.Now.AddDays(-10),
            EndDate = DateTime.Now.AddDays(-1),
            MinimumAmount = 250m,
            RewardPoint = 50m,
            Status = CampaignStatus.Loading,
            IsActive = true
        };

        var (customer, card) = await AddCustomerWithCardAsync(context, campaign, suffix);

        var purchase = NewTransaction(suffix, 0, card, customer, 300m, campaign.StartDate.AddDays(1));
        context.Transactions.Add(purchase);
        await context.SaveChangesAsync();

        // 300 − 60 = 240, below the 250 minimum, so the purchase no longer counts.
        context.Transactions.Add(NewPartialRefund(suffix, 1, purchase, 60m));
        await context.SaveChangesAsync();

        var result = await CreateService(context).CalculateAsync(campaign.Id);

        Assert.Equal(0, result.Value!.QualifyingTransactions);
        Assert.Equal(0m, result.Value.TotalRewardPoint);
    }

    [Fact]
    public async Task ReconcileReversals_ClawsBack_WhenAPartialRefundDropsBelowTheMinimum()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var suffix = DateTime.Now.Ticks.ToString()[^10..];

        var campaign = new Campaign
        {
            Name = "Partial clawback",
            CampaignType = CampaignType.Mass,
            EarningType = EarningType.CardBased,
            StartDate = DateTime.Now.AddDays(-10),
            EndDate = DateTime.Now.AddDays(-1),
            MinimumAmount = 250m,
            RewardPoint = 50m,
            RefundClawbackEnabled = true,
            Status = CampaignStatus.Loading,
            IsActive = true
        };

        var (customer, card) = await AddCustomerWithCardAsync(context, campaign, suffix);

        var first = NewTransaction(suffix, 0, card, customer, 300m, campaign.StartDate.AddDays(1));
        var second = NewTransaction(suffix, 1, card, customer, 300m, campaign.StartDate.AddDays(2));
        context.Transactions.AddRange(first, second);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.CalculateAsync(campaign.Id);   // both count → 100

        // A partial refund drops 'second' to 240, below the minimum, after the reward was loaded.
        context.Transactions.Add(NewPartialRefund(suffix, 2, second, 60m));
        await context.SaveChangesAsync();

        Assert.Equal(1, await service.ReconcileReversalsAsync());

        var net = (await context.CampaignRewards.Where(r => r.CampaignId == campaign.Id).ToListAsync())
            .Sum(r => r.RewardPoint);
        Assert.Equal(50m, net);
    }

    [Fact]
    public async Task ReconcileReversals_ProcessesEachRefundOnce_AndHandlesLaterPartialRefunds()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var suffix = DateTime.Now.Ticks.ToString()[^10..];

        var campaign = new Campaign
        {
            Name = "Incremental partial",
            CampaignType = CampaignType.Mass,
            EarningType = EarningType.CardBased,
            StartDate = DateTime.Now.AddDays(-10),
            EndDate = DateTime.Now.AddDays(-1),
            MinimumAmount = 250m,
            RewardPoint = 50m,
            RefundClawbackEnabled = true,
            Status = CampaignStatus.Loading,
            IsActive = true
        };

        var (customer, card) = await AddCustomerWithCardAsync(context, campaign, suffix);

        var purchase = NewTransaction(suffix, 0, card, customer, 300m, campaign.StartDate.AddDays(1));
        context.Transactions.Add(purchase);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.CalculateAsync(campaign.Id);   // 300 >= 250 -> 50

        decimal Net() => context.CampaignRewards.Where(r => r.CampaignId == campaign.Id).ToList().Sum(r => r.RewardPoint);

        // First partial refund: 300 - 30 = 270, still >= 250, so no clawback — but it is processed.
        context.Transactions.Add(NewPartialRefund(suffix, 1, purchase, 30m));
        await context.SaveChangesAsync();

        Assert.Equal(0, await service.ReconcileReversalsAsync());
        Assert.Equal(50m, Net());
        // Nothing is left unprocessed: the batch will not re-scan that refund.
        Assert.False(await context.Transactions.AnyAsync(
            r => r.OriginalTransactionId != null && r.ClawbackProcessedAt == null));

        // A later partial refund: 300 - 30 - 60 = 210 < 250, so now the purchase drops.
        context.Transactions.Add(NewPartialRefund(suffix, 2, purchase, 60m));
        await context.SaveChangesAsync();

        Assert.Equal(1, await service.ReconcileReversalsAsync());
        Assert.Equal(0m, Net());
    }

    [Fact]
    public async Task ReclaimUnusedPoints_ClawsBackWhateverWasNeverRedeemed()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        // The scenario from the feature request: 200 earned, 100 redeemed, 100 should come back.
        var (campaign, customer, card) = await SeedEndedCampaignWithRewardAsync(
            context, unusedPointsClawbackDays: 30, earned: 200m, loadDate: DateTime.Now.AddDays(-31));

        // Redemption is a "PS" transaction on the card, dated after the campaign ended.
        context.Transactions.Add(NewRedemption(card, customer, 100m));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        Assert.Equal(1, await service.ReclaimUnusedPointsAsync());

        var rows = await context.CampaignRewards.Where(r => r.CampaignId == campaign.Id).ToListAsync();
        var clawback = rows.Single(r => r.RewardType == RewardType.UnusedPointsClawback);
        Assert.Equal(-100m, clawback.RewardPoint);
        Assert.Equal(100m, rows.Sum(r => r.RewardPoint));

        // A second run finds nothing more to do — the campaign was marked processed.
        Assert.Equal(0, await service.ReclaimUnusedPointsAsync());
    }

    [Fact]
    public async Task ReclaimUnusedPoints_SkipsWhenEverythingWasRedeemed()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (campaign, customer, card) = await SeedEndedCampaignWithRewardAsync(
            context, unusedPointsClawbackDays: 30, earned: 200m, loadDate: DateTime.Now.AddDays(-31));

        // The whole 200 spent back as "PS" transactions — nothing left to claw back.
        context.Transactions.Add(NewRedemption(card, customer, 200m));
        await context.SaveChangesAsync();

        Assert.Equal(0, await CreateService(context).ReclaimUnusedPointsAsync());

        var rows = await context.CampaignRewards.Where(r => r.CampaignId == campaign.Id).ToListAsync();
        Assert.DoesNotContain(rows, r => r.RewardType == RewardType.UnusedPointsClawback);
    }

    [Fact]
    public async Task ReclaimUnusedPoints_SkipsBeforeTheWindowCloses()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        // Loaded today; the 30-day window has not closed yet.
        var (campaign, _, _) = await SeedEndedCampaignWithRewardAsync(
            context, unusedPointsClawbackDays: 30, earned: 200m, loadDate: DateTime.Now);

        Assert.Equal(0, await CreateService(context).ReclaimUnusedPointsAsync());

        // Left for a later run rather than marked processed — the window is still open.
        var reloaded = await context.Campaigns.SingleAsync(c => c.Id == campaign.Id);
        Assert.Null(reloaded.UnusedPointsClawbackProcessedAt);
    }

    [Fact]
    public async Task ReclaimUnusedPoints_SkipsACardOnAnExemptProduct()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (campaign, _, _) = await SeedEndedCampaignWithRewardAsync(
            context,
            unusedPointsClawbackDays: 30,
            earned: 200m,
            loadDate: DateTime.Now.AddDays(-31),
            exemptProductIds: [ScenarioBuilder.VisaGoldProductId]);

        // Nothing redeemed at all, but the card's product is exempt, so no clawback is written.
        Assert.Equal(0, await CreateService(context).ReclaimUnusedPointsAsync());

        var rows = await context.CampaignRewards.Where(r => r.CampaignId == campaign.Id).ToListAsync();
        Assert.DoesNotContain(rows, r => r.RewardType == RewardType.UnusedPointsClawback);
    }

    /// <summary>
    /// An Ended campaign with unused-points clawback on, one Earn reward for one customer's
    /// card, loaded on the given date.
    /// </summary>
    private static async Task<(Campaign Campaign, Customer Customer, Card Card)> SeedEndedCampaignWithRewardAsync(
        CampaignDbContext context,
        int unusedPointsClawbackDays,
        decimal earned,
        DateTime loadDate,
        List<int>? exemptProductIds = null)
    {
        var suffix = DateTime.Now.Ticks.ToString()[^10..];

        var campaign = new Campaign
        {
            Name = "Unused points scenario",
            CampaignType = CampaignType.Mass,
            EarningType = EarningType.CardBased,
            StartDate = DateTime.Now.AddDays(-60),
            EndDate = DateTime.Now.AddDays(-30),
            RewardPoint = 50m,
            UnusedPointsClawbackEnabled = true,
            UnusedPointsClawbackDays = unusedPointsClawbackDays,
            Status = CampaignStatus.Ended,
            IsActive = true
        };

        var (customer, card) = await AddCustomerWithCardAsync(context, campaign, suffix);

        context.CampaignRewards.Add(new CampaignReward
        {
            CampaignId = campaign.Id,
            CustomerId = customer.Id,
            CardId = card.Id,
            RewardType = RewardType.Earn,
            QualifyingCount = 1,
            RewardPoint = earned,
            RewardDate = loadDate
        });

        foreach (var productId in exemptProductIds ?? [])
        {
            context.CampaignClawbackExemptProducts.Add(new CampaignClawbackExemptProduct
            {
                CampaignId = campaign.Id,
                ProductId = productId
            });
        }

        await context.SaveChangesAsync();

        return (campaign, customer, card);
    }

    /// <summary>
    /// A card-based campaign paid at 100 (two 300 TL purchases), then one purchase refunded.
    /// The caller sets the clawback fields and, where it matters, shifts the load date before
    /// reconciling.
    /// </summary>
    private static async Task<(Campaign, RewardService)> PaidCampaignWithOneRefundAsync(
        CampaignDbContext context, bool clawbackEnabled, int? clawbackDays)
    {
        var suffix = DateTime.Now.Ticks.ToString()[^10..];

        var campaign = new Campaign
        {
            Name = "Clawback scenario",
            CampaignType = CampaignType.Mass,
            EarningType = EarningType.CardBased,
            StartDate = DateTime.Now.AddDays(-10),
            EndDate = DateTime.Now.AddDays(-1),
            MinimumAmount = 250m,
            RewardPoint = 50m,
            RefundClawbackEnabled = clawbackEnabled,
            RefundClawbackDays = clawbackDays,
            Status = CampaignStatus.Loading,
            IsActive = true
        };

        var (customer, card) = await AddCustomerWithCardAsync(context, campaign, suffix);

        var first = NewTransaction(suffix, 0, card, customer, 300m, campaign.StartDate.AddDays(1));
        var second = NewTransaction(suffix, 1, card, customer, 300m, campaign.StartDate.AddDays(2));
        context.Transactions.AddRange(first, second);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.CalculateAsync(campaign.Id);

        context.Transactions.Add(NewRefund(suffix, 2, second));
        await context.SaveChangesAsync();

        return (campaign, service);
    }

    private static async Task ShiftRewardLoadDateAsync(
        CampaignDbContext context, int campaignId, DateTime loadedAt)
    {
        var earn = await context.CampaignRewards
            .Where(r => r.CampaignId == campaignId && r.RewardType == RewardType.Earn)
            .ToListAsync();

        foreach (var e in earn)
        {
            e.RewardDate = loadedAt;
        }

        await context.SaveChangesAsync();
    }

    private static async Task<(Customer, Card)> AddCustomerWithCardAsync(
        CampaignDbContext context, Campaign campaign, string suffix)
    {
        var customer = new Customer
        {
            CustomerNumber = $"T{suffix}",
            Gender = Gender.Female,
            SegmentId = ScenarioBuilder.FarmerSegmentId,
            IsActive = true
        };

        var card = new Card
        {
            Customer = customer,
            ProductId = ScenarioBuilder.VisaGoldProductId,
            CardType = CardType.Primary,
            IsActive = true
        };

        context.Campaigns.Add(campaign);
        context.Customers.Add(customer);
        context.Cards.Add(card);
        await context.SaveChangesAsync();

        return (customer, card);
    }

    private static Transaction NewTransaction(
        string suffix, int index, Card card, Customer customer, decimal amount, DateTime date) => new()
    {
        Rrn = $"R{suffix}{index:D2}",
        CardId = card.Id,
        CustomerId = customer.Id,
        MerchantId = ScenarioBuilder.OpetMerchantId,
        TransactionCodeId = ScenarioBuilder.SaleTransactionCodeId,
        TransactionDate = date,
        Amount = amount
    };

    /// <summary>
    /// A point-spend row: the "PS" transaction code and a positive amount, dated now — after
    /// the seeded campaign's end date. The unused-points clawback reads these instead of a
    /// separate redemption table.
    /// </summary>
    private static Transaction NewRedemption(Card card, Customer customer, decimal amount) => new()
    {
        Rrn = $"S{DateTime.Now.Ticks}",
        CardId = card.Id,
        CustomerId = customer.Id,
        TransactionCodeId = ScenarioBuilder.RedemptionTransactionCodeId,
        TransactionDate = DateTime.Now,
        Amount = amount
    };

    /// <summary>
    /// A refund row reversing an already-saved purchase: İade code, negative amount, carrying
    /// its own reference and pointing back at the original. The original must already have an id.
    /// </summary>
    private static Transaction NewRefund(string suffix, int index, Transaction original) => new()
    {
        Rrn = $"F{suffix}{index:D2}",
        CardId = original.CardId,
        CustomerId = original.CustomerId,
        MerchantId = original.MerchantId,
        TransactionCodeId = 4,            // İade (IA)
        TransactionDate = DateTime.Now,
        Amount = -original.Amount,
        OriginalTransactionId = original.Id
    };

    /// <summary>A refund of a specific (partial) amount against an already-saved purchase.</summary>
    private static Transaction NewPartialRefund(string suffix, int index, Transaction original, decimal amount) => new()
    {
        Rrn = $"P{suffix}{index:D2}",
        CardId = original.CardId,
        CustomerId = original.CustomerId,
        MerchantId = original.MerchantId,
        TransactionCodeId = 4,            // İade (IA)
        TransactionDate = DateTime.Now,
        Amount = -amount,
        OriginalTransactionId = original.Id
    };
}
