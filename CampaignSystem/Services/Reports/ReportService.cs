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

    public async Task<IReadOnlyList<CampaignMovementDto>> GetCampaignMovementsAsync(
        int campaignId,
        MovementFilter type,
        CancellationToken cancellationToken = default)
    {
        var movements = new List<CampaignMovementDto>();

        var needRewards = type is MovementFilter.All or MovementFilter.Earn or MovementFilter.Clawback;
        var needRefunds = type is MovementFilter.All or MovementFilter.Refund;

        if (needRewards)
        {
            // The reward types the chosen tab asks for.
            var wanted = type switch
            {
                MovementFilter.Earn => new[] { RewardType.Earn },
                MovementFilter.Clawback => new[] { RewardType.Clawback, RewardType.UnusedPointsClawback },
                _ => new[] { RewardType.Earn, RewardType.Clawback, RewardType.UnusedPointsClawback }
            };

            var rewards = await _db.CampaignRewards
                .Where(r => r.CampaignId == campaignId && wanted.Contains(r.RewardType))
                .Select(r => new
                {
                    r.RewardDate,
                    r.RewardType,
                    r.RewardPoint,
                    r.Customer.CustomerNumber
                })
                .ToListAsync(cancellationToken);

            movements.AddRange(rewards.Select(r => new CampaignMovementDto(
                r.RewardDate,
                MovementTypeFor(r.RewardType),
                r.CustomerNumber,
                r.RewardPoint)));
        }

        if (needRefunds)
        {
            var campaign = await _db.Campaigns
                .Where(c => c.Id == campaignId)
                .Select(c => new { c.StartDate, c.EndDate })
                .FirstOrDefaultAsync(cancellationToken);

            if (campaign is not null)
            {
                // Refunds made by this campaign's earners whose original purchase fell in its window.
                var earnerIds = _db.CampaignRewards
                    .Where(r => r.CampaignId == campaignId && r.RewardType == RewardType.Earn)
                    .Select(r => r.CustomerId)
                    .Distinct();

                var refunds = await (
                    from refund in _db.Transactions
                    where refund.OriginalTransactionId != null && earnerIds.Contains(refund.CustomerId)
                    join original in _db.Transactions on refund.OriginalTransactionId equals original.Id
                    where original.TransactionDate >= campaign.StartDate
                          && original.TransactionDate <= campaign.EndDate
                    select new
                    {
                        refund.TransactionDate,
                        refund.Amount,
                        refund.Customer.CustomerNumber
                    })
                    .ToListAsync(cancellationToken);

                movements.AddRange(refunds.Select(r => new CampaignMovementDto(
                    r.TransactionDate,
                    "refund",
                    r.CustomerNumber,
                    Math.Abs(r.Amount))));
            }
        }

        return movements.OrderBy(m => m.Date).ToList();
    }

    private static string MovementTypeFor(RewardType type) => type switch
    {
        RewardType.Clawback => "refundClawback",
        RewardType.UnusedPointsClawback => "unusedClawback",
        _ => "earn"
    };
}
