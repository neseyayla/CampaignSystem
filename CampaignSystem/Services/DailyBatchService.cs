using CampaignSystem.Configuration;
using CampaignSystem.Data;
using CampaignSystem.DTOs;
using CampaignSystem.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CampaignSystem.Services;

/// <summary>
/// The end-of-day job.
///
/// The three steps run in order for a reason: a campaign that starts and ends on the same day
/// is carried all the way through in a single run rather than waiting for tomorrow.
/// </summary>
public class DailyBatchService(
    CampaignDbContext context,
    IRewardService rewardService,
    IOptions<RewardCalculationOptions> options,
    ILogger<DailyBatchService> logger) : IDailyBatchService
{
    public async Task<DailyBatchResultDto> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;

        var result = new DailyBatchResultDto { RunAt = now };

        result.Started = await StartDueCampaignsAsync(now, cancellationToken);
        result.Closed = await CloseFinishedCampaignsAsync(now, cancellationToken);

        await LoadDueRewardsAsync(now, result, cancellationToken);

        logger.LogInformation(
            "Daily batch finished. Started {Started}, closed {Closed}, loaded {Loaded} campaigns, " +
            "wrote {Rewards} rewards worth {Points} points, {Failures} failures.",
            result.Started, result.Closed, result.Loaded,
            result.RewardsCreated, result.TotalRewardPoint, result.Failures.Count);

        return result;
    }

    /// <summary>Pending campaigns whose start date has arrived.</summary>
    private async Task<int> StartDueCampaignsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var due = await context.Campaigns
            .Where(c => c.IsActive
                        && c.Status == CampaignStatus.Pending
                        && c.StartDate <= now)
            .ToListAsync(cancellationToken);

        foreach (var campaign in due)
        {
            campaign.Status = CampaignStatus.Ongoing;
        }

        if (due.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return due.Count;
    }

    /// <summary>
    /// Ongoing campaigns whose end date has passed. They move to Loading, meaning the period
    /// is over and the points are waiting to be granted.
    /// </summary>
    private async Task<int> CloseFinishedCampaignsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var finished = await context.Campaigns
            .Where(c => c.IsActive
                        && c.Status == CampaignStatus.Ongoing
                        && c.EndDate < now)
            .ToListAsync(cancellationToken);

        foreach (var campaign in finished)
        {
            campaign.Status = CampaignStatus.Loading;
        }

        if (finished.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return finished.Count;
    }

    /// <summary>
    /// Loading campaigns whose loading day has arrived. Each one is handed to the reward
    /// service, which writes the rewards and marks the campaign Ended.
    /// </summary>
    private async Task LoadDueRewardsAsync(
        DateTime now,
        DailyBatchResultDto result,
        CancellationToken cancellationToken)
    {
        var cutoff = now.AddDays(-options.Value.DaysAfterCampaignEnd);

        var due = await context.Campaigns
            .Where(c => c.IsActive
                        && c.Status == CampaignStatus.Loading
                        && c.EndDate <= cutoff)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        foreach (var campaignId in due)
        {
            // One campaign failing must not stop the others: a batch that abandons the run
            // halfway leaves customers unpaid with no record of why.
            try
            {
                var calculation = await rewardService.CalculateAsync(campaignId, cancellationToken);

                if (calculation.Status == ResultStatus.Success)
                {
                    result.Loaded++;
                    result.RewardsCreated += calculation.Value!.RewardsCreated;
                    result.TotalRewardPoint += calculation.Value.TotalRewardPoint;
                }
                else
                {
                    result.Failures.Add(new DailyBatchFailureDto
                    {
                        CampaignId = campaignId,
                        Reason = calculation.Error ?? calculation.Status.ToString()
                    });
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Reward loading failed for campaign {CampaignId}.", campaignId);

                result.Failures.Add(new DailyBatchFailureDto
                {
                    CampaignId = campaignId,
                    Reason = exception.Message
                });
            }
        }
    }
}
