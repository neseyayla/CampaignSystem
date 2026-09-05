namespace CampaignSystem.Services;

/// <summary>
/// The nightly reward-maintenance passes that adjust already-granted rewards after a campaign
/// has paid: refund reconciliation and the unused-points sweep. Split out of
/// <see cref="IRewardService"/> so the read/calculate path and the batch settlement path each
/// have one reason to change.
/// </summary>
public interface IRewardReconciliationService
{
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
