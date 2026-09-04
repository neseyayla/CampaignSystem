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

    private static RewardReconciliationService CreateReconciliation(CampaignDbContext context) =>
        new(context, new RewardCalculator(context), NullLogger<RewardReconciliationService>.Instance);

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

    // ── Point clawback settlement ────────────────────────────────────────────
    // One pass, run once per campaign on the day its reward is N days old: a customer keeps
    // only the points they actually spent, everything still unspent is clawed back. A refund
    // needs no special case — its points are simply among the unspent ones.

    [Fact]
    public async Task SettlePointClawback_ClawsBackEverything_WhenNothingWasSpent()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        // 3 × 20 = 60 granted, no redemption, no refund → the whole 60 comes back.
        var (campaign, _, _) = await PaidCampaignAsync(context, clawbackDays: 30, purchaseAmounts: [300m, 300m, 300m]);
        await ShiftRewardLoadDateAsync(context, campaign.Id, DateTime.Now.AddDays(-31));

        Assert.Equal(1, await CreateReconciliation(context).SettlePointClawbackAsync());

        var rows = await context.CampaignRewards.Where(r => r.CampaignId == campaign.Id).ToListAsync();
        Assert.Equal(-60m, rows.Single(r => r.RewardType == RewardType.Clawback).RewardPoint);
        Assert.Equal(0m, rows.Sum(r => r.RewardPoint));
    }

    [Fact]
    public async Task SettlePointClawback_ClawsBackNothing_WhenEveryPointWasSpent()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (campaign, customer, card) = await PaidCampaignAsync(
            context, clawbackDays: 30, purchaseAmounts: [300m, 300m, 300m]);
        await ShiftRewardLoadDateAsync(context, campaign.Id, DateTime.Now.AddDays(-31));

        // The whole 60 spent back as PS transactions after the campaign ended.
        context.Transactions.Add(NewRedemption(card, customer, 60m));
        await context.SaveChangesAsync();

        Assert.Equal(0, await CreateReconciliation(context).SettlePointClawbackAsync());

        var rows = await context.CampaignRewards.Where(r => r.CampaignId == campaign.Id).ToListAsync();
        Assert.DoesNotContain(rows, r => r.RewardType == RewardType.Clawback);
        Assert.Equal(60m, rows.Sum(r => r.RewardPoint));
    }

    [Fact]
    public async Task SettlePointClawback_ClawsBackOnlyTheUnspentRemainder()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (campaign, customer, card) = await PaidCampaignAsync(
            context, clawbackDays: 30, purchaseAmounts: [300m, 300m, 300m]);
        await ShiftRewardLoadDateAsync(context, campaign.Id, DateTime.Now.AddDays(-31));

        // 25 of the 60 spent → 35 comes back, the customer keeps 25.
        context.Transactions.Add(NewRedemption(card, customer, 25m));
        await context.SaveChangesAsync();

        Assert.Equal(1, await CreateReconciliation(context).SettlePointClawbackAsync());

        var rows = await context.CampaignRewards.Where(r => r.CampaignId == campaign.Id).ToListAsync();
        Assert.Equal(-35m, rows.Single(r => r.RewardType == RewardType.Clawback).RewardPoint);
        Assert.Equal(25m, rows.Sum(r => r.RewardPoint));
    }

    [Fact]
    public async Task SettlePointClawback_ClawsBackARefundedPurchasesPoints_WhenNothingWasSpent()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var suffix = DateTime.Now.Ticks.ToString()[^10..];

        var campaign = new Campaign
        {
            Name = "Refund, no spend",
            CampaignType = CampaignType.Mass,
            EarningType = EarningType.CardBased,
            StartDate = DateTime.Now.AddDays(-40),
            EndDate = DateTime.Now.AddDays(-30),
            MinimumAmount = 100m,
            RewardPoint = 20m,
            RefundClawbackEnabled = true,
            RefundClawbackDays = 30,
            Status = CampaignStatus.Loading,
            IsActive = true
        };

        var (customer, card) = await AddCustomerWithCardAsync(context, campaign, suffix);

        var a = NewTransaction(suffix, 0, card, customer, 300m, campaign.StartDate.AddDays(1));
        var b = NewTransaction(suffix, 1, card, customer, 300m, campaign.StartDate.AddDays(2));
        var c = NewTransaction(suffix, 2, card, customer, 300m, campaign.StartDate.AddDays(3));
        context.Transactions.AddRange(a, b, c);
        await context.SaveChangesAsync();

        await CreateService(context).CalculateAsync(campaign.Id);   // Earn 60
        context.Transactions.Add(NewRefund(suffix, 3, c));          // 'c' reversed in full
        await context.SaveChangesAsync();
        await ShiftRewardLoadDateAsync(context, campaign.Id, DateTime.Now.AddDays(-31));

        Assert.Equal(1, await CreateReconciliation(context).SettlePointClawbackAsync());

        // Nothing was spent, so the whole 60 comes back — the refunded purchase's 20 included.
        var rows = await context.CampaignRewards.Where(r => r.CampaignId == campaign.Id).ToListAsync();
        Assert.Equal(-60m, rows.Single(r => r.RewardType == RewardType.Clawback).RewardPoint);
        Assert.Equal(0m, rows.Sum(r => r.RewardPoint));
    }

    [Fact]
    public async Task SettlePointClawback_KeepsSpentPoints_AfterAPartialRefund()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var suffix = DateTime.Now.Ticks.ToString()[^10..];

        var campaign = new Campaign
        {
            Name = "Refund plus spend",
            CampaignType = CampaignType.Mass,
            EarningType = EarningType.CardBased,
            StartDate = DateTime.Now.AddDays(-40),
            EndDate = DateTime.Now.AddDays(-30),
            MinimumAmount = 100m,
            RewardPoint = 20m,
            RefundClawbackEnabled = true,
            RefundClawbackDays = 30,
            Status = CampaignStatus.Loading,
            IsActive = true
        };

        var (customer, card) = await AddCustomerWithCardAsync(context, campaign, suffix);

        var a = NewTransaction(suffix, 0, card, customer, 300m, campaign.StartDate.AddDays(1));
        var b = NewTransaction(suffix, 1, card, customer, 300m, campaign.StartDate.AddDays(2));
        var c = NewTransaction(suffix, 2, card, customer, 300m, campaign.StartDate.AddDays(3));
        context.Transactions.AddRange(a, b, c);
        await context.SaveChangesAsync();

        await CreateService(context).CalculateAsync(campaign.Id);   // Earn 60

        // 'c' drops below the minimum (300 − 250 = 50), and the customer has spent 40 points.
        context.Transactions.Add(NewPartialRefund(suffix, 3, c, 250m));
        context.Transactions.Add(NewRedemption(card, customer, 40m));
        await context.SaveChangesAsync();
        await ShiftRewardLoadDateAsync(context, campaign.Id, DateTime.Now.AddDays(-31));

        Assert.Equal(1, await CreateReconciliation(context).SettlePointClawbackAsync());

        // Keeps the 40 spent, the other 20 (unspent, and no longer earned) comes back.
        var rows = await context.CampaignRewards.Where(r => r.CampaignId == campaign.Id).ToListAsync();
        Assert.Equal(-20m, rows.Single(r => r.RewardType == RewardType.Clawback).RewardPoint);
        Assert.Equal(40m, rows.Sum(r => r.RewardPoint));
    }

    [Fact]
    public async Task SettlePointClawback_ReconcilesRefundInWindow_ThenSweepsTheRestAtDayN()
    {
        // The two-step main scenario: 60 granted, one purchase refunded in full while the
        // window is open — its 20 points come straight back — then on day N the untouched 40
        // is swept because nothing was redeemed.
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var suffix = DateTime.Now.Ticks.ToString()[^10..];

        var campaign = new Campaign
        {
            Name = "Two-step settlement",
            CampaignType = CampaignType.Mass,
            EarningType = EarningType.CardBased,
            StartDate = DateTime.Now.AddDays(-40),
            EndDate = DateTime.Now.AddDays(-30),
            MinimumAmount = 100m,
            RewardPoint = 20m,
            RefundClawbackEnabled = true,
            RefundClawbackDays = 30,
            Status = CampaignStatus.Loading,
            IsActive = true
        };

        var (customer, card) = await AddCustomerWithCardAsync(context, campaign, suffix);

        var a = NewTransaction(suffix, 0, card, customer, 300m, campaign.StartDate.AddDays(1));
        var b = NewTransaction(suffix, 1, card, customer, 300m, campaign.StartDate.AddDays(2));
        var c = NewTransaction(suffix, 2, card, customer, 300m, campaign.StartDate.AddDays(3));
        context.Transactions.AddRange(a, b, c);
        await context.SaveChangesAsync();

        await CreateService(context).CalculateAsync(campaign.Id);   // Earn 60
        context.Transactions.Add(NewRefund(suffix, 3, c));          // 'c' reversed in full
        await context.SaveChangesAsync();

        // Day ~20: inside the 30-day window — step 1 claws the refunded purchase's 20 back.
        await ShiftRewardLoadDateAsync(context, campaign.Id, DateTime.Now.AddDays(-20));
        Assert.Equal(1, await CreateReconciliation(context).SettlePointClawbackAsync());

        var afterRefund = await context.CampaignRewards.Where(r => r.CampaignId == campaign.Id).ToListAsync();
        Assert.Equal(-20m, afterRefund.Single(r => r.RewardType == RewardType.Clawback).RewardPoint);
        Assert.Equal(40m, afterRefund.Sum(r => r.RewardPoint));
        Assert.Null((await context.Campaigns.SingleAsync(x => x.Id == campaign.Id)).UnusedPointsClawbackProcessedAt);

        // Day ~31: window closed — step 2 sweeps the untouched 40.
        await ShiftRewardLoadDateAsync(context, campaign.Id, DateTime.Now.AddDays(-31));
        Assert.Equal(1, await CreateReconciliation(context).SettlePointClawbackAsync());

        var final = await context.CampaignRewards.Where(r => r.CampaignId == campaign.Id).ToListAsync();
        Assert.Equal(2, final.Count(r => r.RewardType == RewardType.Clawback));
        Assert.Equal(0m, final.Sum(r => r.RewardPoint));
    }

    [Fact]
    public async Task SettlePointClawback_SkipsBeforeDayN()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        // Reward loaded today; the 30-day window has not passed.
        var (campaign, _, _) = await PaidCampaignAsync(context, clawbackDays: 30, purchaseAmounts: [300m, 300m, 300m]);

        Assert.Equal(0, await CreateReconciliation(context).SettlePointClawbackAsync());

        var reloaded = await context.Campaigns.SingleAsync(c => c.Id == campaign.Id);
        Assert.Null(reloaded.UnusedPointsClawbackProcessedAt);
        Assert.DoesNotContain(
            await context.CampaignRewards.Where(r => r.CampaignId == campaign.Id).ToListAsync(),
            r => r.RewardType == RewardType.Clawback);
    }

    [Fact]
    public async Task SettlePointClawback_SettlesOncePerCampaign()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (campaign, _, _) = await PaidCampaignAsync(context, clawbackDays: 30, purchaseAmounts: [300m, 300m, 300m]);
        await ShiftRewardLoadDateAsync(context, campaign.Id, DateTime.Now.AddDays(-31));

        Assert.Equal(1, await CreateReconciliation(context).SettlePointClawbackAsync());

        var reloaded = await context.Campaigns.SingleAsync(c => c.Id == campaign.Id);
        Assert.NotNull(reloaded.UnusedPointsClawbackProcessedAt);

        // A second run does nothing — the campaign is marked settled.
        Assert.Equal(0, await CreateReconciliation(context).SettlePointClawbackAsync());
        Assert.Single(
            await context.CampaignRewards
                .Where(r => r.CampaignId == campaign.Id && r.RewardType == RewardType.Clawback)
                .ToListAsync());
    }

    [Fact]
    public async Task SettlePointClawback_SkipsACampaignWithoutTheOptionOn()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (campaign, _, _) = await PaidCampaignAsync(
            context, clawbackDays: null, purchaseAmounts: [300m, 300m, 300m], clawbackEnabled: false);
        await ShiftRewardLoadDateAsync(context, campaign.Id, DateTime.Now.AddDays(-60));

        Assert.Equal(0, await CreateReconciliation(context).SettlePointClawbackAsync());
        Assert.DoesNotContain(
            await context.CampaignRewards.Where(r => r.CampaignId == campaign.Id).ToListAsync(),
            r => r.RewardType == RewardType.Clawback);
    }

    [Fact]
    public async Task SettlePointClawback_IgnoresARefund_WhenTheOptionIsOff()
    {
        // A purchase can be refunded whether or not the campaign reclaims points. With the
        // option off, the refund is recorded but the customer keeps everything they were paid.
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var suffix = DateTime.Now.Ticks.ToString()[^10..];

        var campaign = new Campaign
        {
            Name = "Clawback off, refund lands",
            CampaignType = CampaignType.Mass,
            EarningType = EarningType.CardBased,
            StartDate = DateTime.Now.AddDays(-40),
            EndDate = DateTime.Now.AddDays(-30),
            MinimumAmount = 100m,
            RewardPoint = 20m,
            RefundClawbackEnabled = false,
            Status = CampaignStatus.Loading,
            IsActive = true
        };

        var (customer, card) = await AddCustomerWithCardAsync(context, campaign, suffix);

        var a = NewTransaction(suffix, 0, card, customer, 300m, campaign.StartDate.AddDays(1));
        var b = NewTransaction(suffix, 1, card, customer, 300m, campaign.StartDate.AddDays(2));
        var c = NewTransaction(suffix, 2, card, customer, 300m, campaign.StartDate.AddDays(3));
        context.Transactions.AddRange(a, b, c);
        await context.SaveChangesAsync();

        await CreateService(context).CalculateAsync(campaign.Id);       // Earn 60
        var refund = NewRefund(suffix, 3, c);
        context.Transactions.Add(refund);                               // 'c' reversed in full
        await context.SaveChangesAsync();
        await ShiftRewardLoadDateAsync(context, campaign.Id, DateTime.Now.AddDays(-60));

        Assert.Equal(0, await CreateReconciliation(context).SettlePointClawbackAsync());

        // The refund row is there; the reward is untouched.
        Assert.True(await context.Transactions.AnyAsync(t => t.Id == refund.Id && t.OriginalTransactionId != null));
        var rows = await context.CampaignRewards.Where(r => r.CampaignId == campaign.Id).ToListAsync();
        Assert.DoesNotContain(rows, r => r.RewardType == RewardType.Clawback);
        Assert.Equal(60m, rows.Single(r => r.RewardType == RewardType.Earn).RewardPoint);
    }

    /// <summary>
    /// A card-based campaign taken through reward loading: the purchases are made, the reward
    /// is calculated (which moves the campaign to Ended), and the result is one Earn row per
    /// card. The caller then shifts the load date and adds redemptions or refunds as the
    /// scenario needs.
    /// </summary>
    private static async Task<(Campaign Campaign, Customer Customer, Card Card)> PaidCampaignAsync(
        CampaignDbContext context,
        int? clawbackDays,
        decimal[] purchaseAmounts,
        decimal rewardPoint = 20m,
        decimal minimumAmount = 100m,
        bool clawbackEnabled = true)
    {
        var suffix = DateTime.Now.Ticks.ToString()[^10..];

        var campaign = new Campaign
        {
            Name = "Settlement scenario",
            CampaignType = CampaignType.Mass,
            EarningType = EarningType.CardBased,
            StartDate = DateTime.Now.AddDays(-40),
            EndDate = DateTime.Now.AddDays(-30),
            MinimumAmount = minimumAmount,
            RewardPoint = rewardPoint,
            RefundClawbackEnabled = clawbackEnabled,
            RefundClawbackDays = clawbackDays,
            Status = CampaignStatus.Loading,
            IsActive = true
        };

        var (customer, card) = await AddCustomerWithCardAsync(context, campaign, suffix);

        for (var i = 0; i < purchaseAmounts.Length; i++)
        {
            context.Transactions.Add(
                NewTransaction(suffix, i, card, customer, purchaseAmounts[i], campaign.StartDate.AddDays(1)));
        }

        await context.SaveChangesAsync();

        await CreateService(context).CalculateAsync(campaign.Id);

        return (campaign, customer, card);
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
