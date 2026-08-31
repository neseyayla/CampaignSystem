using CampaignSystem.Configuration;
using CampaignSystem.Data;
using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CampaignSystem.Services;

/// <summary>
/// Decides what each customer earned from a campaign — the live preview, the end-of-campaign
/// calculation, and the summaries and breakdown the screens read.
///
/// Works against the context directly rather than the repository: the results are grouped,
/// summed and written in one transaction, none of which fits behind IRepository.
///
/// The rule that decides which transactions count lives once in
/// <see cref="IRewardCalculator"/>, shared with the batch, so the figure shown to the customer
/// during the campaign and the points actually granted at the end cannot drift apart.
/// </summary>
public class RewardService(
    CampaignDbContext context,
    IRewardCalculator calculator,
    IOptions<RewardCalculationOptions> options,
    ILogger<RewardService> logger) : IRewardService
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

        var qualifying = (await calculator.QualifyingTransactions(campaign, cancellationToken))
            .Where(t => t.CustomerId == customerId);

        var groups = calculator.Group(qualifying, campaign);

        var preview = new RewardPreviewDto
        {
            CampaignId = campaignId,
            CustomerId = customerId,
            Lines = groups.Select(g =>
            {
                var earned = g.Count * (campaign.RewardPoint ?? 0m);
                var granted = calculator.ApplyCap(earned, campaign.MaxRewardAmount);

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

        var qualifying = await calculator.QualifyingTransactions(campaign, cancellationToken);
        var qualifyingCount = qualifying.Count;

        var groups = calculator.Group(qualifying, campaign);

        var now = DateTime.Now;

        var rewards = groups.Select(g => new CampaignReward
        {
            CampaignId = campaignId,
            CustomerId = g.CustomerId,
            CardId = g.CardId,
            QualifyingCount = g.Count,
            RewardPoint = calculator.ApplyCap(g.Count * (campaign.RewardPoint ?? 0m), campaign.MaxRewardAmount),
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

        logger.LogInformation(
            "Campaign {CampaignId} evaluated: {Rewards} reward rows worth {Points} points " +
            "from {Qualifying} qualifying transactions.",
            campaignId, rewards.Count, rewards.Sum(r => r.RewardPoint), qualifyingCount);

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
                TotalRewardPoint = g.Sum(r => r.RewardPoint),
                RewardDate = g.Max(r => r.RewardDate)
            })
            .OrderByDescending(l => l.RewardDate)
            .ToListAsync(cancellationToken);

        // "N işlem" is the count of the customer's purchases the campaign evaluated — the same
        // rows the breakdown lists, refunded ones included — not just those that left a reward
        // row (a purchase dropped at loading time leaves none). So it is derived per campaign the
        // way the breakdown is, scoped to this customer so it does not read everyone's spending.
        var campaignsById = await context.Campaigns
            .AsNoTracking()
            .Where(c => lines.Select(l => l.CampaignId).Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        foreach (var line in lines)
        {
            if (campaignsById.TryGetValue(line.CampaignId, out var campaign))
            {
                line.QualifyingCount = (await calculator.QualifyingTransactions(
                    campaign, cancellationToken, includeReversed: true, customerId: customerId)).Count;
            }
        }

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
        var lines = (await calculator.QualifyingTransactions(
                campaign, cancellationToken, includeReversed: true, customerId: customerId))
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

        // The refund rows themselves — amount and date — not just the fact that one exists, so
        // the screen can list each İade and show the running effect it had. Grouped per purchase,
        // oldest first.
        var lineIds = lines.Select(t => t.Id).ToList();
        var refundsByPurchase = (await context.Transactions
                .Where(r => r.OriginalTransactionId != null && lineIds.Contains(r.OriginalTransactionId.Value))
                .Select(r => new { OriginalId = r.OriginalTransactionId!.Value, r.Amount, r.TransactionDate, r.ClawbackProcessedAt })
                .ToListAsync(cancellationToken))
            .GroupBy(r => r.OriginalId)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.TransactionDate).ToList());

        var point = campaign.RewardPoint ?? 0m;

        return new RewardBreakdownDto
        {
            CampaignId = campaign.Id,
            CampaignName = campaign.Name,
            RewardPointPerTransaction = point,
            MinimumAmount = campaign.MinimumAmount,
            Lines = lines.Select(t =>
            {
                var refunds = refundsByPurchase.GetValueOrDefault(t.Id, []);
                var effective = t.Amount + refunds.Sum(r => r.Amount);

                // The same rule the reward engine applies: a purchase keeps its points while its
                // amount net of refunds stays positive and at or above the minimum. So a partial
                // refund that leaves it qualifying is not "reversed" — only one that drops it is.
                var stillQualifies = effective > 0m
                    && (campaign.MinimumAmount is null || effective >= campaign.MinimumAmount);

                // Shown red only once the points are actually gone, so the breakdown never
                // disagrees with the balance. While the campaign runs that is the moment a refund
                // drops the purchase (the live preview). Once it has ended and paid out, the
                // points leave only when the nightly batch reconciles the refund — until then the
                // purchase still counts, even though the İade is already visible beneath it.
                var settled = campaign.Status != CampaignStatus.Ended
                    || refunds.All(r => r.ClawbackProcessedAt is not null);

                return new RewardBreakdownLineDto
                {
                    TransactionId = t.Id,
                    TransactionDate = t.TransactionDate,
                    Amount = t.Amount,
                    MerchantName = t.MerchantId is not null
                        ? merchantNames.GetValueOrDefault(t.MerchantId.Value)
                        : null,
                    RewardPoint = point,
                    EffectiveAmount = effective,
                    IsReversed = refunds.Count > 0 && !stillQualifies && settled,
                    Refunds = refunds
                        .Select(r => new RefundLineDto { Date = r.TransactionDate, Amount = r.Amount })
                        .ToList()
                };
            }).ToList()
        };
    }

}
