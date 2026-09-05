using CampaignSystem.Data;
using CampaignSystem.Entities;
using CampaignSystem.Enums;
using Microsoft.EntityFrameworkCore;

namespace CampaignSystem.Services;

/// <summary>
/// The nightly reward-maintenance passes that run after a campaign has paid: refund
/// reconciliation — a recurring recheck while the refund window is open — and the unused-points
/// sweep — a one-time pass once the redemption window closes. Both adjust already-granted
/// CAMPAIGN_REWARD rows rather than recomputing a campaign from scratch.
///
/// Split out of <see cref="RewardService"/> so the read/calculate path and this batch
/// settlement path each change for their own reasons. Reconciliation reuses the shared
/// <see cref="IRewardCalculator"/>, so "what should this group hold now" is decided by the very
/// same rule the original calculation used.
/// </summary>
public class RewardReconciliationService(
    CampaignDbContext context,
    IRewardCalculator calculator,
    ILogger<RewardReconciliationService> logger) : IRewardReconciliationService
{
    /// <summary>
    /// Transaction code that marks a row as a spend of campaign points. The unused-points
    /// clawback sums these to find what a card earned but never redeemed.
    /// </summary>
    private const string RedemptionTransactionCode = "PS";

    public async Task<int> ReconcileReversalsAsync(CancellationToken cancellationToken = default)
    {
        // Only refunds the batch has not yet accounted for drive any work. Already-processed
        // refunds are never re-scanned; a later partial refund arrives as a fresh unprocessed row
        // and is handled then. The processed flag gates the work, never the maths: the effective
        // amount below still sums every refund, processed or not.
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

        // Campaigns that reclaim points, have paid, and paid one of the customers with a new
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
                "Refund clawback: wrote {Clawbacks} clawback rows across {Campaigns} campaigns " +
                "from {Refunds} new refunds.",
                clawbacks, campaigns.Count, pendingRefunds.Count);
        }

        return clawbacks;
    }

    // Reconcile one already-paid campaign against the refunds seen this run: write a negative
    // Clawback row for every group whose net now sits above what the campaign should pay.
    // Returns how many clawback rows it staged (saved by the caller).
    private async Task<int> ReconcileCampaignAsync(
        Campaign campaign, DateTime today, DateTime now, CancellationToken cancellationToken)
    {
        var rewards = await context.CampaignRewards
            .Where(r => r.CampaignId == campaign.Id)
            .ToListAsync(cancellationToken);

        // The window runs from the day the reward was loaded — every Earn row of a campaign
        // shares that date. Once it has passed, a refund is settled and the campaign is left
        // alone. Null days means no limit.
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

            // A reversal can only lower the net. When it has dropped, record the shortfall as
            // a negative Clawback row rather than editing the Earn row. When it has not, do
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

    public async Task<int> ReclaimUnusedPointsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Now.Date;
        var now = DateTime.Now;

        // Ended campaigns with the rule on that have not been swept yet. Once a campaign is
        // processed here it is never looked at again — ProcessedAt is what makes this a
        // one-time sweep rather than a recheck like ReconcileReversalsAsync above.
        var campaigns = await context.Campaigns
            .Include(c => c.ClawbackExemptProducts)
            .Where(c => c.IsActive
                        && c.Status == CampaignStatus.Ended
                        && c.UnusedPointsClawbackEnabled
                        && c.UnusedPointsClawbackProcessedAt == null)
            .ToListAsync(cancellationToken);

        var reclaimed = 0;

        foreach (var campaign in campaigns)
        {
            reclaimed += await ReclaimForCampaignAsync(campaign, today, now, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);

        return reclaimed;
    }

    // Sweep one ended campaign for points that were earned but never redeemed inside the
    // window: write a negative UnusedPointsClawback row for each non-exempt group with an
    // unspent balance, and stamp ProcessedAt once the campaign is settled. Returns how many
    // clawback rows it staged (saved by the caller).
    private async Task<int> ReclaimForCampaignAsync(
        Campaign campaign, DateTime today, DateTime now, CancellationToken cancellationToken)
    {
        var rewards = await context.CampaignRewards
            .Where(r => r.CampaignId == campaign.Id)
            .ToListAsync(cancellationToken);

        var earnRows = rewards.Where(r => r.RewardType == RewardType.Earn).ToList();

        if (earnRows.Count == 0)
        {
            // Nobody qualified when the batch ran — nothing to reclaim, and nothing will
            // ever change that, so this campaign is done.
            campaign.UnusedPointsClawbackProcessedAt = now;
            return 0;
        }

        // The window runs from the day the reward was loaded — every Earn row of a
        // campaign shares that date, the same rule ReconcileReversalsAsync uses above.
        var loadDate = earnRows.Max(r => r.RewardDate).Date;
        var deadline = loadDate.AddDays(campaign.UnusedPointsClawbackDays!.Value);

        if (today < deadline)
        {
            // Window still open — leave ProcessedAt null so a later run picks this up.
            return 0;
        }

        // Net balance per group (customer, or customer+card for a card based campaign),
        // the same rollup ReconcileReversalsAsync uses to find what a group currently holds.
        var groups = rewards
            .GroupBy(r => (r.CustomerId, r.CardId))
            .Select(g => new GroupBalance(g.Key.CustomerId, g.Key.CardId, g.Sum(r => r.RewardPoint)))
            .Where(g => g.NetPoint > 0)
            .ToList();

        var reclaimed = await WriteUnusedPointClawbacksAsync(campaign, groups, now, cancellationToken);

        campaign.UnusedPointsClawbackProcessedAt = now;
        return reclaimed;
    }

    // Given the groups that still hold points, drop the exempt ones and the points already
    // redeemed, and write a negative UnusedPointsClawback row for whatever is left unspent.
    // Returns how many clawback rows it staged (saved by the caller).
    private async Task<int> WriteUnusedPointClawbacksAsync(
        Campaign campaign, IReadOnlyList<GroupBalance> groups, DateTime now, CancellationToken cancellationToken)
    {
        var exemptProductIds = campaign.ClawbackExemptProducts.Select(x => x.ProductId).ToHashSet();

        // Card based rewards carry the specific card; exemption is checked against that
        // card's product. Customer based rewards pool every card (CardId is null), so
        // there is no single card to check — a customer is exempt there if any one of
        // their cards is on an exempt product, which is the only reading that makes sense
        // once the reward is no longer tied to one card.
        var cardIds = groups.Where(g => g.CardId != null).Select(g => g.CardId!.Value).Distinct().ToList();

        var cardProducts = await context.Cards
            .Where(c => cardIds.Contains(c.Id))
            .Select(c => new { c.Id, c.ProductId })
            .ToDictionaryAsync(c => c.Id, c => c.ProductId, cancellationToken);

        var customerIds = groups.Select(g => g.CustomerId).Distinct().ToList();

        HashSet<int> customerHasExemptCard = exemptProductIds.Count == 0
            ? []
            : (await context.Cards
                .Where(c => customerIds.Contains(c.CustomerId) && exemptProductIds.Contains(c.ProductId))
                .Select(c => c.CustomerId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();

        // Points spent show up on TRANSACTION as rows with the "PS" code. A transaction
        // carries no campaign link, so redemption is matched by card rather than by
        // campaign: a card based group nets its own card's PS spend; a customer based
        // group (CardId null, every card pooled) nets the PS spend of all the customer's
        // cards — the same reading the exemption check above uses. Only spending dated
        // from the campaign's end through now counts.
        var psSpend = await context.Transactions
            .AsNoTracking()
            .Where(t => t.TransactionCode.Code == RedemptionTransactionCode
                        && t.TransactionDate >= campaign.EndDate
                        && t.TransactionDate <= now
                        && customerIds.Contains(t.CustomerId))
            .Select(t => new { t.CustomerId, t.CardId, t.Amount })
            .ToListAsync(cancellationToken);

        var redeemedByCard = psSpend
            .GroupBy(t => t.CardId)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

        var redeemedByCustomer = psSpend
            .GroupBy(t => t.CustomerId)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

        var reclaimed = 0;

        foreach (var g in groups)
        {
            var exempt = g.CardId is int cardId
                ? cardProducts.TryGetValue(cardId, out var productId) && exemptProductIds.Contains(productId)
                : customerHasExemptCard.Contains(g.CustomerId);

            if (exempt)
            {
                continue;
            }

            var redeemed = g.CardId is int redeemedCardId
                ? redeemedByCard.GetValueOrDefault(redeemedCardId, 0m)
                : redeemedByCustomer.GetValueOrDefault(g.CustomerId, 0m);

            var unused = g.NetPoint - redeemed;

            if (unused > 0)
            {
                context.CampaignRewards.Add(new CampaignReward
                {
                    CampaignId = campaign.Id,
                    CustomerId = g.CustomerId,
                    CardId = g.CardId,
                    RewardType = RewardType.UnusedPointsClawback,
                    QualifyingCount = 0,
                    RewardPoint = -unused,
                    RewardDate = now
                });

                reclaimed++;
            }
        }

        return reclaimed;
    }

    // A campaign group that still holds points: the customer, the card (null when the reward
    // pools every card), and its current net balance. The contract between the sweep's gate
    // and the clawback-writing step.
    private sealed record GroupBalance(int CustomerId, int? CardId, decimal NetPoint);
}
