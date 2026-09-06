using CampaignSystem.Data;
using CampaignSystem.DTOs.Reports;
using CampaignSystem.Enums;
using Microsoft.EntityFrameworkCore;

namespace CampaignSystem.Services.Reports;

/// <summary>
/// Read-only reporting over the reward and transaction tables. Injects the DbContext directly,
/// like the other aggregation-heavy services (rewards, batch), rather than going through the
/// generic repository, which is meant for simple CRUD.
/// </summary>
public class ReportService : IReportService
{
    private readonly CampaignDbContext _db;

    public ReportService(CampaignDbContext db)
    {
        _db = db;
    }

    public async Task<CampaignReportDto?> GetCampaignReportByIdAsync(int campaignId, CancellationToken cancellationToken = default)
    {
        var campaign = await _db.Campaigns
            .AsNoTracking()
            .Where(c => c.Id == campaignId)
            .Select(c => new { c.Id, c.Name, c.StartDate, c.EndDate })
            .FirstOrDefaultAsync(cancellationToken);

        if (campaign is null) return null;

        var rewards = _db.CampaignRewards.Where(r => r.CampaignId == campaign.Id);
        var earnRewards = rewards.Where(r => r.RewardType == RewardType.Earn);

        var customerCount = await earnRewards
            .Select(r => r.CustomerId)
            .Distinct()
            .CountAsync(cancellationToken);

        var totalPointsEarned = await earnRewards
            .SumAsync(r => (decimal?)r.RewardPoint, cancellationToken) ?? 0m;

        // Clawback and UnusedPointsClawback rows both carry negative points; report the magnitude.
        var totalPointsClawedBack = await rewards
            .Where(r => r.RewardType != RewardType.Earn)
            .SumAsync(r => (decimal?)r.RewardPoint, cancellationToken) ?? 0m;

        // Refunds whose original purchase falls inside the campaign window and belongs to a
        // customer who earned in this campaign. Amount is negative on a refund; report the
        // magnitude of what was returned.
        var earnerIds = earnRewards.Select(r => r.CustomerId).Distinct();

        var totalRefundAmount = await (
            from refund in _db.Transactions
            where refund.OriginalTransactionId != null && earnerIds.Contains(refund.CustomerId)
            join original in _db.Transactions on refund.OriginalTransactionId equals original.Id
            where original.TransactionDate >= campaign.StartDate
                  && original.TransactionDate <= campaign.EndDate
            select (decimal?)refund.Amount)
            .SumAsync(cancellationToken) ?? 0m;

        return new CampaignReportDto(
            campaign.Id,
            campaign.Name,
            customerCount,
            totalPointsEarned,
            Math.Abs(totalPointsClawedBack),
            Math.Abs(totalRefundAmount));
    }
}
