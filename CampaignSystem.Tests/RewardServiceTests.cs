using CampaignSystem.Configuration;
using CampaignSystem.Data;
using CampaignSystem.Entities;
using CampaignSystem.Enums;
using CampaignSystem.Services;
using CampaignSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
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
        new(context, Options.Create(new RewardCalculationOptions
        {
            DaysAfterCampaignEnd = daysAfterCampaignEnd
        }));

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
}
