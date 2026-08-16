using CampaignSystem.DTOs;

namespace CampaignSystem.Services;

public interface IRewardService
{
    /// <summary>
    /// What the customer would earn if the campaign were evaluated now. Writes nothing, so
    /// it can be called as often as the customer refreshes the screen.
    /// </summary>
    Task<ServiceResult<RewardPreviewDto>> PreviewAsync(
        int campaignId,
        int customerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the end-of-campaign batch: finds the qualifying transactions, groups them
    /// according to the campaign's earning type, applies the cap and writes CAMPAIGN_REWARD.
    /// Runs once per campaign — a second call is refused rather than recalculated.
    /// </summary>
    Task<ServiceResult<RewardCalculationResultDto>> CalculateAsync(
        int campaignId,
        CancellationToken cancellationToken = default);

    /// <summary>Rewards already written for a campaign. Null when the campaign is not found.</summary>
    Task<List<RewardDto>?> GetByCampaignAsync(
        int campaignId,
        CancellationToken cancellationToken = default);
}
