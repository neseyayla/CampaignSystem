namespace CampaignSystem.Services;

/// <summary>
/// Point clawback after a campaign has paid. Split out of <see cref="IRewardService"/> so the
/// read/calculate path and the batch settlement path each have one reason to change.
/// </summary>
public interface IRewardReconciliationService
{
    /// <summary>
    /// Runs both clawback steps for every ended campaign with the "Puan geri alımı" option on:
    /// refund reconciliation (each batch while inside the N-day window, a refunded purchase's
    /// points come straight back) and the unspent sweep (once, on the day the reward turns
    /// <see cref="Entities.Campaign.RefundClawbackDays"/> days old — guarded by
    /// <see cref="Entities.Campaign.UnusedPointsClawbackProcessedAt"/> — the customer keeps only
    /// the points they redeemed and the rest is clawed back). Returns how many clawback rows
    /// were written.
    /// </summary>
    Task<int> SettlePointClawbackAsync(CancellationToken cancellationToken = default);
}
