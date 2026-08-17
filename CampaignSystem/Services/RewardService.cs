using CampaignSystem.Data;
using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Enums;
using Microsoft.EntityFrameworkCore;

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
public class RewardService(CampaignDbContext context) : IRewardService
{
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

        if (campaign.Status != CampaignStatus.Ongoing)
        {
            return ServiceResult<RewardCalculationResultDto>.Invalid(
                $"Only an Ongoing campaign can be evaluated; this one is {campaign.Status}.");
        }

        if (campaign.EndDate > DateTime.Now)
        {
            return ServiceResult<RewardCalculationResultDto>.Invalid(
                $"The campaign runs until {campaign.EndDate:yyyy-MM-dd} and cannot be evaluated yet.");
        }

        // Belt and braces: the status check above already covers this, but rewards are
        // money and a duplicate run must never be able to slip through.
        if (await context.CampaignRewards.AnyAsync(r => r.CampaignId == campaignId, cancellationToken))
        {
            return ServiceResult<RewardCalculationResultDto>.Conflict(
                "This campaign has already been evaluated. Recalculating would rewrite rewards that customers have been given.");
        }

        campaign.Status = CampaignStatus.Loading;

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

    /// <summary>
    /// The transactions that meet every one of the campaign's conditions.
    ///
    /// A criteria table with no rows for the campaign is not a filter that matches nothing —
    /// it means the campaign places no restriction on that dimension. Each criterion is
    /// therefore only applied when the campaign actually names something.
    /// </summary>
    private async Task<List<Transaction>> QualifyingTransactions(
        Campaign campaign,
        CancellationToken cancellationToken)
    {
        var segmentIds = await context.CampaignSegments
            .Where(x => x.CampaignId == campaign.Id).Select(x => x.SegmentId).ToListAsync(cancellationToken);

        var productIds = await context.CampaignProducts
            .Where(x => x.CampaignId == campaign.Id).Select(x => x.ProductId).ToListAsync(cancellationToken);

        var merchantIds = await context.CampaignMerchants
            .Where(x => x.CampaignId == campaign.Id).Select(x => x.MerchantId).ToListAsync(cancellationToken);

        var transactionCodeIds = await context.CampaignTransactionCodes
            .Where(x => x.CampaignId == campaign.Id).Select(x => x.TransactionCodeId).ToListAsync(cancellationToken);

        var query = context.Transactions
            .AsNoTracking()
            .Where(t => t.TransactionDate >= campaign.StartDate && t.TransactionDate <= campaign.EndDate);

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

        // Enrollment campaigns reach only the customers who signed up, and only through the
        // level they signed up at: a card level enrollment covers that one card, a customer
        // level enrollment covers all of them.
        if (campaign.CampaignType == CampaignType.EnrollmentRequired)
        {
            var enrolments = await context.CampaignParticipations
                .Where(p => p.CampaignId == campaign.Id && p.Status == ParticipationStatus.Active)
                .Select(p => new { p.CustomerId, p.CardId })
                .ToListAsync(cancellationToken);

            var customerLevel = enrolments.Where(e => e.CardId == null)
                .Select(e => e.CustomerId).ToList();

            var cardLevel = enrolments.Where(e => e.CardId != null)
                .Select(e => e.CardId!.Value).ToList();

            query = query.Where(t => customerLevel.Contains(t.CustomerId) || cardLevel.Contains(t.CardId));
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Groups the qualifying transactions at the level the campaign accumulates: one group
    /// per card, or one per customer with every card pooled.
    /// </summary>
    private static List<RewardGroup> Group(IEnumerable<Transaction> transactions, Campaign campaign)
        => campaign.EarningType == EarningType.CardBased
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
