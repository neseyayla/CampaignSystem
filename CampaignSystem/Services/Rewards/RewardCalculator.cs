using CampaignSystem.Data;
using CampaignSystem.Entities;
using CampaignSystem.Enums;
using Microsoft.EntityFrameworkCore;

namespace CampaignSystem.Services;

/// <summary>
/// The reward calculation core, extracted from RewardService so the one rule that decides
/// what a campaign pays on has a single home: which transactions qualify
/// (<see cref="QualifyingTransactions"/>), how they roll up into per-card or per-customer
/// groups (<see cref="Group"/>) capped at the campaign's ceiling (<see cref="ApplyCap"/>).
///
/// Works against the context directly — the criteria span four junction tables the
/// transactions are filtered through, which does not fit behind IRepository.
/// </summary>
public class RewardCalculator(CampaignDbContext context) : IRewardCalculator
{
    public async Task<List<Transaction>> QualifyingTransactions(
        Campaign campaign,
        CancellationToken cancellationToken,
        bool includeReversed = false,
        int? customerId = null)
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

        // Scoped to one customer when the caller only needs theirs — the customer breakdown and
        // the "N işlem" figure on the summary — so those do not load every customer's spending.
        if (customerId is not null)
        {
            query = query.Where(t => t.CustomerId == customerId.Value);
        }

        query = ApplyAmountFilters(query, campaign, includeReversed);

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

        if (campaign.CampaignType == CampaignType.EnrollmentRequired)
        {
            return await FilterByEnrollmentAsync(query, campaign, cancellationToken);
        }

        return await query.ToListAsync(cancellationToken);
    }

    // Maximum always tests the original amount — a purchase too large to qualify is not
    // rescued by a partial refund. Minimum, and being non-zero, test the amount net of
    // refunds: a partial refund drops a purchase only when the remainder falls below the
    // minimum, a full refund takes it to zero and out ("refunded" is derived from the refund
    // rows, not a stored flag). The breakdown (includeReversed) instead keeps every
    // criteria-matching purchase — refunded ones are shown apart — so there minimum tests the
    // original amount too and no refunded row is dropped.
    private IQueryable<Transaction> ApplyAmountFilters(
        IQueryable<Transaction> query, Campaign campaign, bool includeReversed)
    {
        if (includeReversed)
        {
            if (campaign.MinimumAmount is not null)
            {
                query = query.Where(t => t.Amount >= campaign.MinimumAmount);
            }

            if (campaign.MaximumAmount is not null)
            {
                query = query.Where(t => t.Amount <= campaign.MaximumAmount);
            }

            return query;
        }

        if (campaign.MaximumAmount is not null)
        {
            query = query.Where(t => t.Amount <= campaign.MaximumAmount);
        }

        query = query.Where(t =>
            t.Amount + (context.Transactions
                .Where(r => r.OriginalTransactionId == t.Id)
                .Sum(r => (decimal?)r.Amount) ?? 0m) > 0m);

        if (campaign.MinimumAmount is not null)
        {
            query = query.Where(t =>
                t.Amount + (context.Transactions
                    .Where(r => r.OriginalTransactionId == t.Id)
                    .Sum(r => (decimal?)r.Amount) ?? 0m) >= campaign.MinimumAmount);
        }

        return query;
    }

    // Enrollment campaigns reach only the customers who signed up, and only through the
    // level they signed up at: a card level enrollment covers that one card, a customer
    // level enrollment covers all of them.
    private async Task<List<Transaction>> FilterByEnrollmentAsync(
        IQueryable<Transaction> query,
        Campaign campaign,
        CancellationToken cancellationToken)
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

    public List<RewardGroup> Group(IEnumerable<Transaction> transactions, Campaign campaign)
        => campaign.AccumulatesPerCard
            ? transactions
                .GroupBy(t => new { t.CustomerId, t.CardId })
                .Select(g => new RewardGroup(g.Key.CustomerId, g.Key.CardId, g.Count()))
                .ToList()
            : transactions
                .GroupBy(t => t.CustomerId)
                .Select(g => new RewardGroup(g.Key, null, g.Count()))
                .ToList();

    public decimal ApplyCap(decimal earned, decimal? cap)
        => cap is null ? earned : Math.Min(earned, cap.Value);
}
