using CampaignSystem.Data;
using CampaignSystem.Entities;
using CampaignSystem.Enums;
using Microsoft.EntityFrameworkCore;

namespace CampaignSystem.Services;

/// <summary>
/// Point clawback after a campaign has paid. One option on the campaign ("Puan geri alımı"
/// + N days), settled in two steps:
///
///  1. <b>Refund reconciliation</b> — runs on every batch while the campaign is still inside
///     its N-day window. A refund (full or partial) that drops a counted purchase lowers what
///     the customer was entitled to, and the difference is clawed back straight away.
///
///  2. <b>Unspent sweep</b> — runs once, on the day the reward turns N days old. Of whatever
///     the customer still holds after step 1, only the part they actually redeemed is theirs;
///     the rest is clawed back. A customer can never have spent more than step 1 left them, so
///     this never drives a balance below zero.
///
/// Split out of <see cref="RewardService"/> so the read/calculate path and this batch
/// settlement path each change for their own reasons. Step 1 reuses the shared
/// <see cref="IRewardCalculator"/>, so "what should this group hold now" is decided by the
/// very same rule the original calculation used.
/// </summary>
public class RewardReconciliationService(
    CampaignDbContext context,
    IRewardCalculator calculator,
    ILogger<RewardReconciliationService> logger) : IRewardReconciliationService
{
    /// <summary>
    /// Transaction code that marks a row as a spend of campaign points. The unspent sweep sums
    /// these to find what a customer redeemed and keeps.
    /// </summary>
    private const string RedemptionTransactionCode = "PS";

    public async Task<int> SettlePointClawbackAsync(CancellationToken cancellationToken = default)
    {
        var clawbacks = 0;

        clawbacks += await ReconcileRefundsAsync(cancellationToken);
        clawbacks += await SweepUnspentAsync(cancellationToken);

        return clawbacks;
    }

    // ── Step 1: refund reconciliation (every batch, inside the N-day window) ──────────────

    private async Task<int> ReconcileRefundsAsync(CancellationToken cancellationToken)
    {
        // Only refunds the batch has not yet accounted for drive any work. Already-processed
        // refunds are never re-scanned; a later partial refund arrives as a fresh unprocessed
        // row and is handled then. The processed flag gates the work, never the maths: the
        // effective amount below still sums every refund, processed or not.
        var pendingRefunds = await context.Transactions
            .Where(r => r.OriginalTransactionId != null && r.ClawbackProcessedAt == null)
            .ToListAsync(cancellationToken);

        if (pendingRefunds.Count == 0)
        {
            return 0;
        }

        var pendingCustomers = pendingRefunds.Select(r => r.CustomerId).Distinct().ToList();

        var today = DateTime.Now.Date;
        var now = DateTime.Now;

        // Campaigns with the clawback option on that have paid one of the customers with a new
        // refund — every other campaign is skipped entirely.
        var campaigns = await context.Campaigns
            .Where(c => c.IsActive
                        && c.Status == CampaignStatus.Ended
                        && c.RefundClawbackEnabled
                        && c.Rewards.Any(r => r.RewardType == RewardType.Earn
                                              && pendingCustomers.Contains(r.CustomerId)))
            .ToListAsync(cancellationToken);

        var clawbacks = 0;

        foreach (var campaign in campaigns)
        {
            clawbacks += await ReconcileCampaignAsync(campaign, today, now, cancellationToken);
        }

        // Every refund seen this run is now accounted for. Marking them keeps the next run from
        // re-scanning them; a purchase's later refunds arrive as new unprocessed rows.
        foreach (var refund in pendingRefunds)
        {
            refund.ClawbackProcessedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);

        if (clawbacks > 0)
        {
            logger.LogInformation(
                "Refund clawback: wrote {Clawbacks} rows across {Campaigns} campaigns from {Refunds} new refunds.",
                clawbacks, campaigns.Count, pendingRefunds.Count);
        }

        return clawbacks;
    }

    // Reconcile one already-paid campaign against the refunds seen this run: write a negative
    // Clawback row for every group whose net now sits above what the campaign should pay.
    private async Task<int> ReconcileCampaignAsync(
        Campaign campaign, DateTime today, DateTime now, CancellationToken cancellationToken)
    {
        var rewards = await context.CampaignRewards
            .Where(r => r.CampaignId == campaign.Id)
            .ToListAsync(cancellationToken);

        // The window runs from the day the reward was loaded — every Earn row shares that date.
        // Once N days have passed a refund is settled and the campaign is left to the sweep.
        var loadDate = rewards
            .Where(r => r.RewardType == RewardType.Earn)
            .Max(r => r.RewardDate)
            .Date;

        if (campaign.RefundClawbackDays is int days && today > loadDate.AddDays(days))
        {
            return 0;
        }

        // What each group should be now, with refunded purchases left out.
        var correct = calculator.Group(await calculator.QualifyingTransactions(campaign, cancellationToken), campaign)
            .ToDictionary(
                g => (g.CustomerId, g.CardId),
                g => (Count: g.Count,
                      Point: calculator.ApplyCap(g.Count * (campaign.RewardPoint ?? 0m), campaign.MaxRewardAmount)));

        // The net of every row so far — the Earn row plus any earlier Clawback rows — per group.
        var groups = rewards
            .GroupBy(r => (r.CustomerId, r.CardId))
            .Select(g => new
            {
                g.Key.CustomerId,
                g.Key.CardId,
                NetPoint = g.Sum(r => r.RewardPoint),
                NetCount = g.Sum(r => r.QualifyingCount)
            });

        var clawbacks = 0;

        foreach (var g in groups)
        {
            var c = correct.GetValueOrDefault((g.CustomerId, g.CardId), (Count: 0, Point: 0m));

            // A reversal can only lower the net. When it has dropped, record the shortfall as a
            // negative Clawback row rather than editing the Earn row. When it has not, do
            // nothing — which also makes a second run over the same refunds a no-op.
            if (g.NetPoint > c.Point)
            {
                context.CampaignRewards.Add(new CampaignReward
                {
                    CampaignId = campaign.Id,
                    CustomerId = g.CustomerId,
                    CardId = g.CardId,
                    RewardType = RewardType.Clawback,
                    QualifyingCount = c.Count - g.NetCount,   // negative
                    RewardPoint = c.Point - g.NetPoint,       // negative
                    RewardDate = now
                });

                clawbacks++;
            }
        }

        return clawbacks;
    }

    // ── Step 2: unspent sweep (once, on the day the reward is N days old) ─────────────────

    private async Task<int> SweepUnspentAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.Now.Date;
        var now = DateTime.Now;

        // Ended campaigns with the option on that have not been swept yet. ProcessedAt is what
        // makes this a one-time settlement rather than a recurring recheck.
        var campaigns = await context.Campaigns
            .Where(c => c.IsActive
                        && c.Status == CampaignStatus.Ended
                        && c.RefundClawbackEnabled
                        && c.UnusedPointsClawbackProcessedAt == null)
            .ToListAsync(cancellationToken);

        var clawbacks = 0;

        foreach (var campaign in campaigns)
        {
            clawbacks += await SweepCampaignAsync(campaign, today, now, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);

        if (clawbacks > 0)
        {
            logger.LogInformation(
                "Unspent-point sweep: settled {Campaigns} campaigns, wrote {Clawbacks} rows.",
                campaigns.Count, clawbacks);
        }

        return clawbacks;
    }

    // Sweep one campaign once its reward is N days old: a customer keeps only what they
    // redeemed, and a negative Clawback row takes the rest. Then stamp ProcessedAt.
    private async Task<int> SweepCampaignAsync(
        Campaign campaign, DateTime today, DateTime now, CancellationToken cancellationToken)
    {
        var rewards = await context.CampaignRewards
            .Where(r => r.CampaignId == campaign.Id)
            .ToListAsync(cancellationToken);

        var earnRows = rewards.Where(r => r.RewardType == RewardType.Earn).ToList();

        if (earnRows.Count == 0)
        {
            // Nobody qualified when the batch ran — nothing to sweep, ever.
            campaign.UnusedPointsClawbackProcessedAt = now;
            return 0;
        }

        // N days is counted from the day the reward was loaded. Null (which the DTO does not
        // allow while the option is on) is treated as sweep-on-load-day.
        var loadDate = earnRows.Max(r => r.RewardDate).Date;

        if (today < loadDate.AddDays(campaign.RefundClawbackDays ?? 0))
        {
            // Not yet — leave ProcessedAt null so a later run picks this up.
            return 0;
        }

        // Net balance per group after step 1: the Earn row plus any refund Clawback rows.
        var groups = rewards
            .GroupBy(r => (r.CustomerId, r.CardId))
            .Select(g => new GroupBalance(g.Key.CustomerId, g.Key.CardId, g.Sum(r => r.RewardPoint)))
            .Where(g => g.NetPoint > 0)
            .ToList();

        if (groups.Count == 0)
        {
            campaign.UnusedPointsClawbackProcessedAt = now;
            return 0;
        }

        // Points spent show up on TRANSACTION as rows with the "PS" code. A transaction carries
        // no campaign link, so redemption is matched by card rather than by campaign: a
        // card-based group nets its own card's PS spend; a customer-based group (CardId null,
        // every card pooled) nets the PS spend of all the customer's cards. Only spending dated
        // from the campaign's end through now counts.
        var customerIds = groups.Select(g => g.CustomerId).Distinct().ToList();

        var psSpend = await context.Transactions
            .AsNoTracking()
            .Where(t => t.TransactionCode.Code == RedemptionTransactionCode
                        && t.TransactionDate >= campaign.EndDate
                        && t.TransactionDate <= now
                        && customerIds.Contains(t.CustomerId))
            .Select(t => new { t.CustomerId, t.CardId, t.Amount })
            .ToListAsync(cancellationToken);

        var spentByCard = psSpend
            .GroupBy(t => t.CardId)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

        var spentByCustomer = psSpend
            .GroupBy(t => t.CustomerId)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

        var clawbacks = 0;

        foreach (var g in groups)
        {
            var spent = g.CardId is int cardId
                ? spentByCard.GetValueOrDefault(cardId, 0m)
                : spentByCustomer.GetValueOrDefault(g.CustomerId, 0m);

            // The customer keeps what they spent; the unspent rest comes back. Never negative —
            // step 1 already caps what they could have spent, and if recorded spend somehow
            // exceeds the balance (points from another campaign on the same card in this
            // window), nothing is written and the balance stays as it is.
            var clawback = g.NetPoint - spent;

            if (clawback > 0)
            {
                context.CampaignRewards.Add(new CampaignReward
                {
                    CampaignId = campaign.Id,
                    CustomerId = g.CustomerId,
                    CardId = g.CardId,
                    RewardType = RewardType.Clawback,
                    QualifyingCount = 0,
                    RewardPoint = -clawback,
                    RewardDate = now
                });

                clawbacks++;
            }
        }

        campaign.UnusedPointsClawbackProcessedAt = now;
        return clawbacks;
    }

    // A campaign group and its net balance after refund reconciliation — the input to the
    // unspent sweep.
    private sealed record GroupBalance(int CustomerId, int? CardId, decimal NetPoint);
}
