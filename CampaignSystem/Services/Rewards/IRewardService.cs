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

    /// <summary>
    /// A campaign's rewards rolled up per customer, so the several rows a card based
    /// campaign produces for one customer appear as one figure.
    /// Null when the campaign is not found.
    /// </summary>
    Task<CampaignRewardSummaryDto?> GetCampaignSummaryAsync(
        int campaignId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Everything one customer has earned, grouped by campaign.
    /// Null when the customer is not found.
    /// </summary>
    Task<CustomerRewardSummaryDto?> GetCustomerSummaryAsync(
        int customerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The purchases behind one campaign's reward for a customer, in date order, each earning
    /// or reversed, for the drill-down under "Kazandıklarım". Null when the campaign is not
    /// found; an empty line list when the customer earned nothing from it.
    /// </summary>
    Task<RewardBreakdownDto?> GetRewardBreakdownAsync(
        int customerId,
        int campaignId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recomputes every campaign whose rewards were loaded within the refund window and, where
    /// a reversed transaction has lowered what a customer should have earned, reduces the
    /// stored reward. Reconciliation only ever lowers a reward; it never raises one. Returns
    /// how many reward rows were adjusted.
    /// </summary>
    Task<int> ReconcileReversalsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sweeps every ended campaign whose unused-points window has closed and claws back
    /// whatever each customer (or card) earned but never redeemed. Runs once per campaign —
    /// unlike <see cref="ReconcileReversalsAsync"/> this is a one-time pass, not a recurring
    /// recheck, guarded by <see cref="Entities.Campaign.UnusedPointsClawbackProcessedAt"/>.
    /// Returns how many clawback rows were written.
    /// </summary>
    Task<int> ReclaimUnusedPointsAsync(CancellationToken cancellationToken = default);
}
