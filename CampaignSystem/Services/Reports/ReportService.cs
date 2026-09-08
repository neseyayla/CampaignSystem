using CampaignSystem.Data;
using CampaignSystem.DTOs.Reports;
using CampaignSystem.Enums;
using Microsoft.EntityFrameworkCore;

namespace CampaignSystem.Services.Reports;

/// <summary>
/// Read-only reporting over the reward tables. Injects the DbContext directly, like the other
/// aggregation-heavy services (rewards, batch), rather than going through the generic repository,
/// which is meant for simple CRUD.
/// </summary>
public class ReportService : IReportService
{
    private readonly CampaignDbContext _db;

    public ReportService(CampaignDbContext db)
    {
        _db = db;
    }

    private static readonly CampaignReportResultDto Empty =
        new([], new ReportTotalsDto(0, 0, 0m, 0m, 0m));

    public async Task<CampaignReportResultDto> GetCampaignSummariesAsync(
        int? campaignId,
        ClawbackFilter clawback,
        CancellationToken cancellationToken = default)
    {
        // The eligible campaigns first, narrowed by the report's filters in the database. Only
        // these campaigns' rewards are then aggregated, so no filtering happens in memory.
        var campaignQuery = _db.Campaigns.AsNoTracking();

        if (campaignId is not null)
            campaignQuery = campaignQuery.Where(c => c.Id == campaignId);

        campaignQuery = clawback switch
        {
            ClawbackFilter.Refund => campaignQuery.Where(c => c.RefundClawbackEnabled),
            ClawbackFilter.Unused => campaignQuery.Where(c => c.UnusedPointsClawbackEnabled),
            ClawbackFilter.Both => campaignQuery.Where(c => c.RefundClawbackEnabled && c.UnusedPointsClawbackEnabled),
            _ => campaignQuery
        };

        var campaigns = await campaignQuery
            .Select(c => new { c.Id, c.Name, c.RefundClawbackEnabled, c.UnusedPointsClawbackEnabled })
            .ToListAsync(cancellationToken);

        if (campaigns.Count == 0) return Empty;

        var ids = campaigns.Select(c => c.Id).ToList();

        // Point totals per campaign in one grouped query. Conditional sums become SUM(CASE WHEN),
        // so the three reward types are split without a second pass. Both clawback sums are
        // negative in the table; the projection below reports their magnitude.
        var points = await _db.CampaignRewards
            .Where(r => ids.Contains(r.CampaignId))
            .GroupBy(r => r.CampaignId)
            .Select(g => new
            {
                CampaignId = g.Key,
                Earned = g.Sum(r => r.RewardType == RewardType.Earn ? r.RewardPoint : 0m),
                RefundClawback = g.Sum(r => r.RewardType == RewardType.Clawback ? r.RewardPoint : 0m),
                UnusedClawback = g.Sum(r => r.RewardType == RewardType.UnusedPointsClawback ? r.RewardPoint : 0m)
            })
            .ToListAsync(cancellationToken);

        // Distinct customers per campaign. Selecting the distinct (campaign, customer) pairs first
        // keeps this translatable, unlike a Distinct().Count() nested inside the group above.
        var customerCounts = await _db.CampaignRewards
            .Where(r => ids.Contains(r.CampaignId) && r.RewardType == RewardType.Earn)
            .Select(r => new { r.CampaignId, r.CustomerId })
            .Distinct()
            .GroupBy(x => x.CampaignId)
            .Select(g => new { CampaignId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var campaignById = campaigns.ToDictionary(c => c.Id);
        var countById = customerCounts.ToDictionary(c => c.CampaignId, c => c.Count);

        var rows = points
            .Select(p =>
            {
                var refundClawback = Math.Abs(p.RefundClawback);
                var unusedClawback = Math.Abs(p.UnusedClawback);
                var campaign = campaignById[p.CampaignId];
                return new CampaignReportSummaryDto(
                    p.CampaignId,
                    campaign.Name,
                    countById.GetValueOrDefault(p.CampaignId, 0),
                    p.Earned,
                    refundClawback,
                    unusedClawback,
                    p.Earned - refundClawback - unusedClawback,
                    campaign.RefundClawbackEnabled,
                    campaign.UnusedPointsClawbackEnabled);
            })
            .OrderBy(s => s.CampaignName)
            .ToList();

        // Distinct across the whole filtered set: a customer in several campaigns is counted once,
        // unlike the per-campaign counts summed on the cards would be.
        var distinctCustomers = await _db.CampaignRewards
            .Where(r => ids.Contains(r.CampaignId) && r.RewardType == RewardType.Earn)
            .Select(r => r.CustomerId)
            .Distinct()
            .CountAsync(cancellationToken);

        var totals = new ReportTotalsDto(
            rows.Count,
            distinctCustomers,
            rows.Sum(r => r.TotalPointsEarned),
            rows.Sum(r => r.RefundClawbackPoints + r.UnusedClawbackPoints),
            rows.Sum(r => r.NetPoints));

        return new CampaignReportResultDto(rows, totals);
    }

    public async Task<IReadOnlyList<CampaignLedgerLineDto>> GetCampaignLedgerAsync(
        int campaignId,
        CancellationToken cancellationToken = default)
    {
        // Reward rows aggregated by type: how many, the point total and the date span.
        var rewardAgg = await _db.CampaignRewards
            .Where(r => r.CampaignId == campaignId)
            .GroupBy(r => r.RewardType)
            .Select(g => new
            {
                Type = g.Key,
                Count = g.Count(),
                Total = g.Sum(r => r.RewardPoint),
                First = (DateTime?)g.Min(r => r.RewardDate),
                Last = (DateTime?)g.Max(r => r.RewardDate)
            })
            .ToListAsync(cancellationToken);

        var byType = rewardAgg.ToDictionary(x => x.Type);

        // The load line counts distinct customers (matching the table), not reward rows — a
        // card-based campaign writes one Earn row per card, so rows can exceed customers.
        var earnCustomers = await _db.CampaignRewards
            .Where(r => r.CampaignId == campaignId && r.RewardType == RewardType.Earn)
            .Select(r => r.CustomerId)
            .Distinct()
            .CountAsync(cancellationToken);

        CampaignLedgerLineDto RewardLine(string type, RewardType rewardType, int? countOverride = null)
        {
            byType.TryGetValue(rewardType, out var a);
            return new CampaignLedgerLineDto(type, countOverride ?? a?.Count ?? 0, a?.Total ?? 0m, a?.First, a?.Last);
        }

        // Refund line (money): refunds by this campaign's earners whose original fell in its window.
        var refundCount = 0;
        var refundTotal = 0m;
        DateTime? refundFirst = null, refundLast = null;

        var campaign = await _db.Campaigns
            .Where(c => c.Id == campaignId)
            .Select(c => new { c.StartDate, c.EndDate })
            .FirstOrDefaultAsync(cancellationToken);

        if (campaign is not null)
        {
            var earnerIds = _db.CampaignRewards
                .Where(r => r.CampaignId == campaignId && r.RewardType == RewardType.Earn)
                .Select(r => r.CustomerId)
                .Distinct();

            var refundRows = await (
                from refund in _db.Transactions
                where refund.OriginalTransactionId != null && earnerIds.Contains(refund.CustomerId)
                join original in _db.Transactions on refund.OriginalTransactionId equals original.Id
                where original.TransactionDate >= campaign.StartDate
                      && original.TransactionDate <= campaign.EndDate
                select new { refund.Amount, refund.TransactionDate })
                .ToListAsync(cancellationToken);

            refundCount = refundRows.Count;
            refundTotal = refundRows.Sum(r => Math.Abs(r.Amount));
            if (refundCount > 0)
            {
                refundFirst = refundRows.Min(r => r.TransactionDate);
                refundLast = refundRows.Max(r => r.TransactionDate);
            }
        }

        return new List<CampaignLedgerLineDto>
        {
            RewardLine("earn", RewardType.Earn, earnCustomers),
            new("refund", refundCount, refundTotal, refundFirst, refundLast),
            RewardLine("refundClawback", RewardType.Clawback),
            RewardLine("unusedClawback", RewardType.UnusedPointsClawback)
        };
    }
}
