using CampaignSystem.Configuration;
using CampaignSystem.Data;
using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CampaignSystem.Services;

/// <summary>
/// Decides what each customer earned from a campaign.
///
/// Works against the context directly rather than the repository: the criteria live in
/// four junction tables, the transactions have to be grouped and summed, and the whole
/// result has to be written in one transaction. None of that fits behind IRepository.
///
/// The rule that decides which transactions count is written once, in
/// <see cref="QualifyingTransactions"/>, and used by both the preview and the batch. If it
/// were written twice, the figure shown to the customer during the campaign and the points
/// actually granted at the end would eventually disagree.
/// </summary>
public class RewardService(
    CampaignDbContext context,
    IOptions<RewardCalculationOptions> options) : IRewardService
{
    private int DaysAfterCampaignEnd => options.Value.DaysAfterCampaignEnd;

    public async Task<ServiceResult<RewardPreviewDto>> PreviewAsync(
        int campaignId,
        int customerId,
        CancellationToken cancellationToken = default)
    {
        var campaign = await context.Campaigns
            .FirstOrDefaultAsync(c => c.Id == campaignId && c.IsActive, cancellationToken);

        if (campaign is null)
        {
            return ServiceResult<RewardPreviewDto>.NotFound();
        }

        if (!await context.Customers.AnyAsync(c => c.Id == customerId && c.IsActive, cancellationToken))
        {
            return ServiceResult<RewardPreviewDto>.Invalid($"Unknown or inactive customer id: {customerId}.");
        }

        var qualifying = (await QualifyingTransactions(campaign, cancellationToken))
            .Where(t => t.CustomerId == customerId);

        var groups = Group(qualifying, campaign);

        var preview = new RewardPreviewDto
        {
            CampaignId = campaignId,
            CustomerId = customerId,
            Lines = groups.Select(g =>
            {
                var earned = g.Count * (campaign.RewardPoint ?? 0m);
                var granted = ApplyCap(earned, campaign.MaxRewardAmount);

                return new RewardPreviewLineDto
                {
                    CardId = g.CardId,
                    QualifyingCount = g.Count,
                    EarnedBeforeCap = earned,
                    RewardPoint = granted,
                    CapApplied = granted < earned
                };
            }).ToList()
        };

        preview.TotalRewardPoint = preview.Lines.Sum(l => l.RewardPoint);

        return ServiceResult<RewardPreviewDto>.Success(preview);
    }

    public async Task<ServiceResult<RewardCalculationResultDto>> CalculateAsync(
        int campaignId,
        CancellationToken cancellationToken = default)
    {
        var campaign = await context.Campaigns
            .FirstOrDefaultAsync(c => c.Id == campaignId && c.IsActive, cancellationToken);

        if (campaign is null)
        {
            return ServiceResult<RewardCalculationResultDto>.NotFound();
        }

        // Rewards are calculated once the campaign period is over. Loading is exactly that
        // state: ended, not yet paid. Pending and Ongoing are too early, Ended is too late.
        if (campaign.Status != CampaignStatus.Loading)
        {
            return ServiceResult<RewardCalculationResultDto>.Invalid(
                $"Rewards are calculated once a campaign has ended and is awaiting loading; this one is {campaign.Status}.");
        }

        // The day the rewards fall due, counted in days rather than in hours: a campaign that
        // ends at 23:59 on the 19th is five days old on the 24th, not on the 25th.
        var loadingDate = campaign.EndDate.Date.AddDays(DaysAfterCampaignEnd);

        if (DateTime.Now.Date < loadingDate)
        {
            return ServiceResult<RewardCalculationResultDto>.Invalid(
                $"Rewards for this campaign are loaded on {loadingDate:yyyy-MM-dd}, which has not arrived yet.");
        }

        // Belt and braces: the status check above already covers this, but rewards are
        // money and a duplicate run must never be able to slip through.
        if (await context.CampaignRewards.AnyAsync(r => r.CampaignId == campaignId, cancellationToken))
        {
            return ServiceResult<RewardCalculationResultDto>.Conflict(
                "This campaign has already been evaluated. Recalculating would rewrite rewards that customers have been given.");
        }

        var qualifying = await QualifyingTransactions(campaign, cancellationToken);
        var qualifyingCount = qualifying.Count;

        var groups = Group(qualifying, campaign);

        var now = DateTime.Now;

        var rewards = groups.Select(g => new CampaignReward
        {
            CampaignId = campaignId,
            CustomerId = g.CustomerId,
            CardId = g.CardId,
            QualifyingCount = g.Count,
            RewardPoint = ApplyCap(g.Count * (campaign.RewardPoint ?? 0m), campaign.MaxRewardAmount),
            RewardDate = now
        }).ToList();

        context.CampaignRewards.AddRange(rewards);

        // The one status worth storing: that the batch has run. Nothing in the dates can
        // tell us this, least of all for a campaign where nobody qualified and no reward
        // rows were written.
        campaign.Status = CampaignStatus.Ended;

        // The rewards and the closing status are written together. A crash in between would
        // otherwise leave a campaign that looks unevaluated but already has rows.
        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<RewardCalculationResultDto>.Success(new RewardCalculationResultDto
        {
            CampaignId = campaignId,
            QualifyingTransactions = qualifyingCount,
            RewardsCreated = rewards.Count,
            TotalRewardPoint = rewards.Sum(r => r.RewardPoint),
            CalculatedAt = now
        });
    }

    public async Task<List<RewardDto>?> GetByCampaignAsync(
        int campaignId,
        CancellationToken cancellationToken = default)
    {
        if (!await context.Campaigns.AnyAsync(c => c.Id == campaignId && c.IsActive, cancellationToken))
        {
            return null;
        }

        return await context.CampaignRewards
            .AsNoTracking()
            .Where(r => r.CampaignId == campaignId)
            .Select(r => new RewardDto
            {
                Id = r.Id,
                CampaignId = r.CampaignId,
                CustomerId = r.CustomerId,
                CardId = r.CardId,
                QualifyingCount = r.QualifyingCount,
                RewardPoint = r.RewardPoint,
                RewardDate = r.RewardDate
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CampaignRewardSummaryDto?> GetCampaignSummaryAsync(
        int campaignId,
        CancellationToken cancellationToken = default)
    {
        var campaign = await context.Campaigns
            .AsNoTracking()
            .Where(c => c.Id == campaignId && c.IsActive)
            .Select(c => new { c.Id, c.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (campaign is null)
        {
            return null;
        }

        // Grouped by the database rather than in memory: only one row per customer comes
        // back instead of every reward row.
        var lines = await context.CampaignRewards
            .AsNoTracking()
            .Where(r => r.CampaignId == campaignId)
            .GroupBy(r => new { r.CustomerId, r.Customer.CustomerNumber })
            .Select(g => new CampaignRewardCustomerLineDto
            {
                CustomerId = g.Key.CustomerId,
                CustomerNumber = g.Key.CustomerNumber,
                RewardRows = g.Count(),
                QualifyingCount = g.Sum(r => r.QualifyingCount),
                TotalRewardPoint = g.Sum(r => r.RewardPoint)
            })
            .OrderByDescending(l => l.TotalRewardPoint)
            .ToListAsync(cancellationToken);

        return new CampaignRewardSummaryDto
        {
            CampaignId = campaign.Id,
            CampaignName = campaign.Name,
            CustomerCount = lines.Count,
            TotalRewardPoint = lines.Sum(l => l.TotalRewardPoint),
            Customers = lines
        };
    }

    public async Task<CustomerRewardSummaryDto?> GetCustomerSummaryAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        var customer = await context.Customers
            .AsNoTracking()
            .Where(c => c.Id == customerId && c.IsActive)
            .Select(c => new { c.Id, c.CustomerNumber })
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            return null;
        }

        var lines = await context.CampaignRewards
            .AsNoTracking()
            .Where(r => r.CustomerId == customerId)
            .GroupBy(r => new { r.CampaignId, r.Campaign.Name })
            .Select(g => new CustomerRewardCampaignLineDto
            {
                CampaignId = g.Key.CampaignId,
                CampaignName = g.Key.Name,
                RewardRows = g.Count(),
                QualifyingCount = g.Sum(r => r.QualifyingCount),
                TotalRewardPoint = g.Sum(r => r.RewardPoint),
                RewardDate = g.Max(r => r.RewardDate)
            })
            .OrderByDescending(l => l.RewardDate)
            .ToListAsync(cancellationToken);

        return new CustomerRewardSummaryDto
        {
            CustomerId = customer.Id,
            CustomerNumber = customer.CustomerNumber,
            TotalRewardPoint = lines.Sum(l => l.TotalRewardPoint),
            Campaigns = lines
        };
    }

    public async Task<RewardBreakdownDto?> GetRewardBreakdownAsync(
        int customerId,
        int campaignId,
        CancellationToken cancellationToken = default)
    {
        var campaign = await context.Campaigns
            .FirstOrDefaultAsync(c => c.Id == campaignId && c.IsActive, cancellationToken);

        if (campaign is null)
        {
            return null;
        }

        // Reversed purchases are kept so refunds can be shown in red alongside the earners.
        var lines = (await QualifyingTransactions(campaign, cancellationToken, includeReversed: true))
            .Where(t => t.CustomerId == customerId)
            .OrderBy(t => t.TransactionDate)
            .ToList();

        var merchantIds = lines
            .Where(t => t.MerchantId is not null)
            .Select(t => t.MerchantId!.Value)
            .Distinct()
            .ToList();

        var merchantNames = await context.Merchants
            .Where(m => merchantIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.MerchantName, cancellationToken);

        // A purchase shows red when a refund row points at it — derived here, not a stored flag.
        var lineIds = lines.Select(t => t.Id).ToList();
        var refundedIds = (await context.Transactions
                .Where(r => r.OriginalTransactionId != null && lineIds.Contains(r.OriginalTransactionId.Value))
                .Select(r => r.OriginalTransactionId!.Value)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var point = campaign.RewardPoint ?? 0m;

        return new RewardBreakdownDto
        {
            CampaignId = campaign.Id,
            CampaignName = campaign.Name,
            RewardPointPerTransaction = point,
            Lines = lines.Select(t => new RewardBreakdownLineDto
            {
                TransactionId = t.Id,
                TransactionDate = t.TransactionDate,
                Amount = t.Amount,
                MerchantName = t.MerchantId is not null
                    ? merchantNames.GetValueOrDefault(t.MerchantId.Value)
                    : null,
                RewardPoint = point,
                IsReversed = refundedIds.Contains(t.Id)
            }).ToList()
        };
    }

    public async Task<int> ReconcileReversalsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Now.Date;

        // Campaigns that reclaim points and have actually paid.
        var campaigns = await context.Campaigns
            .Where(c => c.IsActive
                        && c.Status == CampaignStatus.Ended
                        && c.RefundClawbackEnabled
                        && c.Rewards.Any(r => r.RewardType == RewardType.Earn))
            .ToListAsync(cancellationToken);

        var clawbacks = 0;
        var now = DateTime.Now;

        foreach (var campaign in campaigns)
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
                continue;
            }

            // What each group should be now, with refunded purchases left out.
            var correct = Group(await QualifyingTransactions(campaign, cancellationToken), campaign)
                .ToDictionary(
                    g => (g.CustomerId, g.CardId),
                    g => (Count: g.Count,
                          Point: ApplyCap(g.Count * (campaign.RewardPoint ?? 0m), campaign.MaxRewardAmount)));

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
        }

        if (clawbacks > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return clawbacks;
    }

    /// <summary>
    /// The transactions that meet every one of the campaign's conditions.
    ///
    /// A criteria table with no rows for the campaign is not a filter that matches nothing —
    /// it means the campaign places no restriction on that dimension. Each criterion is
    /// therefore only applied when the campaign actually names something.
    /// </summary>
    private async Task<List<Transaction>> QualifyingTransactions(
        Campaign campaign,
        CancellationToken cancellationToken,
        bool includeReversed = false)
    {
        var segmentIds = await context.CampaignSegments
            .Where(x => x.CampaignId == campaign.Id).Select(x => x.SegmentId).ToListAsync(cancellationToken);

        var productIds = await context.CampaignProducts
            .Where(x => x.CampaignId == campaign.Id).Select(x => x.ProductId).ToListAsync(cancellationToken);

        var merchantIds = await context.CampaignMerchants
            .Where(x => x.CampaignId == campaign.Id).Select(x => x.MerchantId).ToListAsync(cancellationToken);

        var transactionCodeIds = await context.CampaignTransactionCodes
            .Where(x => x.CampaignId == campaign.Id).Select(x => x.TransactionCodeId).ToListAsync(cancellationToken);

        // Capped at now as well as at the campaign's end: a transaction dated in the future
        // has not happened yet, so the live preview must not pay on it. The batch runs after a
        // campaign closes, when every transaction in the window is already in the past, so this
        // cap only ever trims the preview — never the settled figure.
        var now = DateTime.Now;

        var query = context.Transactions
            .AsNoTracking()
            .Where(t => t.TransactionDate >= campaign.StartDate
                     && t.TransactionDate <= campaign.EndDate
                     && t.TransactionDate <= now
                     // A refund row (the negative İade transaction) is never a purchase and
                     // never earns; only the original it reverses is ever in scope.
                     && t.OriginalTransactionId == null);

        // A refunded purchase no longer earns, so it is dropped from the first calculation and
        // every recalculation. "Refunded" is not a stored flag — it is true when a refund row
        // points at this transaction, derived here. The breakdown screen is the one caller that
        // keeps refunded purchases (to show them in red), so it asks for them explicitly.
        if (!includeReversed)
        {
            query = query.Where(t => !context.Transactions.Any(r => r.OriginalTransactionId == t.Id));
        }

        if (campaign.MinimumAmount is not null)
        {
            query = query.Where(t => t.Amount >= campaign.MinimumAmount);
        }

        if (campaign.MaximumAmount is not null)
        {
            query = query.Where(t => t.Amount <= campaign.MaximumAmount);
        }

        if (merchantIds.Count > 0)
        {
            query = query.Where(t => t.MerchantId != null && merchantIds.Contains(t.MerchantId.Value));
        }

        if (transactionCodeIds.Count > 0)
        {
            query = query.Where(t => transactionCodeIds.Contains(t.TransactionCodeId));
        }

        if (productIds.Count > 0)
        {
            query = query.Where(t => productIds.Contains(t.Card.ProductId));
        }

        if (segmentIds.Count > 0)
        {
            query = query.Where(t =>
                t.Customer.SegmentId != null && segmentIds.Contains(t.Customer.SegmentId.Value));
        }

        // The demographic filters follow the same rule as the criteria tables: a campaign
        // that says nothing about gender or card type places no restriction on it.
        //
        // A customer whose gender was never recorded, or a card with no type, is excluded
        // once the campaign narrows on that field — an unknown value cannot be shown to
        // match, and paying on a guess is worse than not paying.
        if (campaign.Gender is not null)
        {
            query = query.Where(t => t.Customer.Gender == campaign.Gender);
        }

        if (campaign.CardType is not null)
        {
            query = query.Where(t => t.Card.CardType == campaign.CardType);
        }

        // Enrollment campaigns reach only the customers who signed up, and only through the
        // level they signed up at: a card level enrollment covers that one card, a customer
        // level enrollment covers all of them.
        if (campaign.CampaignType == CampaignType.EnrollmentRequired)
        {
            var enrolments = await context.CampaignParticipations
                .Where(p => p.CampaignId == campaign.Id && p.Status == ParticipationStatus.Active)
                .Select(p => new { p.CustomerId, p.CardId, p.ParticipationDate })
                .ToListAsync(cancellationToken);

            // Keyed by the level the customer signed up at — one card, or every card at
            // customer level — and by the earliest active enrollment date, so re-joining
            // never moves the start forward.
            var customerLevelFrom = enrolments
                .Where(e => e.CardId == null)
                .GroupBy(e => e.CustomerId)
                .ToDictionary(g => g.Key, g => g.Min(e => e.ParticipationDate));

            var cardLevelFrom = enrolments
                .Where(e => e.CardId != null)
                .GroupBy(e => e.CardId!.Value)
                .ToDictionary(g => g.Key, g => g.Min(e => e.ParticipationDate));

            // Set on the campaign definition screen: whether a customer only earns from the
            // day they joined, or — having joined at all — earns on everything in the
            // campaign's window. The query above already bounds every candidate to the
            // campaign's date range, so "from the campaign period" only has to stop treating
            // the join date itself as a cutoff.
            Func<DateTime, DateTime> cutoff = campaign.EnrollmentBasis == EnrollmentBasis.CampaignPeriod
                ? joinedOn => campaign.StartDate
                : joinedOn => joinedOn;

            // The date cut is per enrollment, so the membership test moves in memory: EF cannot
            // translate "is this transaction after this particular customer's join date".
            var candidates = await query.ToListAsync(cancellationToken);

            return candidates
                .Where(t =>
                    (customerLevelFrom.TryGetValue(t.CustomerId, out var customerFrom)
                        && t.TransactionDate >= cutoff(customerFrom))
                    || (cardLevelFrom.TryGetValue(t.CardId, out var cardFrom)
                        && t.TransactionDate >= cutoff(cardFrom)))
                .ToList();
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Groups the qualifying transactions at the level the campaign accumulates: one group
    /// per card, or one per customer with every card pooled.
    /// </summary>
    private static List<RewardGroup> Group(IEnumerable<Transaction> transactions, Campaign campaign)
        => campaign.AccumulatesPerCard
            ? transactions
                .GroupBy(t => new { t.CustomerId, t.CardId })
                .Select(g => new RewardGroup(g.Key.CustomerId, g.Key.CardId, g.Count()))
                .ToList()
            : transactions
                .GroupBy(t => t.CustomerId)
                .Select(g => new RewardGroup(g.Key, null, g.Count()))
                .ToList();

    private static decimal ApplyCap(decimal earned, decimal? cap)
        => cap is null ? earned : Math.Min(earned, cap.Value);

    /// <summary>One reward's worth of transactions. CardId is null at customer level.</summary>
    private record RewardGroup(int CustomerId, int? CardId, int Count);
}
