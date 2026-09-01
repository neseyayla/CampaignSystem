using CampaignSystem.Configuration;
using CampaignSystem.Data;
using CampaignSystem.DTOs;
using CampaignSystem.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CampaignSystem.Services;

/// <summary>
/// Heuristic implementation of <see cref="ICampaignRecommendationService"/>.
///
/// Three signals per merchant category, blended into one score:
///   spend   — net card spend over the lookback window, normalised against the busiest category
///   trend   — the recent half of that window against the half before it
///   season  — the average seasonal weight over the months the suggested campaign would run
/// A category no open or upcoming campaign already targets is multiplied by a configurable
/// boost, because surfacing those gaps is the point.
///
/// Works against the context directly: the figures come from grouping the transaction table
/// by category and cross-referencing the campaign-merchant table, which is not what a
/// repository is for.
/// </summary>
public class CampaignRecommendationService(
    CampaignDbContext context,
    IOptions<RecommendationOptions> options) : ICampaignRecommendationService
{
    private readonly RecommendationOptions _options = options.Value;

    public async Task<List<CampaignSuggestionDto>> GetSuggestionsAsync(
        RecommendationQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var lookbackDays = Math.Clamp(query.LookbackDays ?? _options.LookbackDays, 14, 365);
        var horizonDays = Math.Clamp(query.HorizonDays ?? _options.HorizonDays, 7, 180);
        var minimumSpend = Math.Max(0m, query.MinimumSpend ?? _options.MinimumSpend);
        var maxSuggestions = Math.Clamp(query.MaxSuggestions ?? _options.MaxSuggestions, 1, 50);

        var now = DateTime.Now;
        var windowStart = now.AddDays(-lookbackDays);
        var midPoint = now.AddDays(-lookbackDays / 2.0);
        var horizonEnd = now.AddDays(horizonDays);

        // One row per category: net spend (purchases are positive, refund rows negative, so a
        // plain Sum nets them) split at the midpoint, plus the purchase-only figures the
        // suggested reward is sized from.
        var aggregates = await context.Transactions
            .AsNoTracking()
            .Where(t => t.MerchantId != null
                        && t.TransactionDate >= windowStart
                        && t.TransactionDate < now)
            .GroupBy(t => t.Merchant!.MerchantCategoryId)
            .Select(g => new CategoryAggregate
            {
                CategoryId = g.Key,
                RecentSpend = g.Sum(x => x.TransactionDate >= midPoint ? x.Amount : 0m),
                PriorSpend = g.Sum(x => x.TransactionDate < midPoint ? x.Amount : 0m),
                PurchaseSpend = g.Sum(x => x.OriginalTransactionId == null ? x.Amount : 0m),
                PurchaseCount = g.Sum(x => x.OriginalTransactionId == null ? 1 : 0)
            })
            .ToListAsync(cancellationToken);

        if (aggregates.Count == 0)
        {
            return [];
        }

        var categoryNames = await context.MerchantCategories
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.CategoryName, cancellationToken);

        var activeMerchantsByCategory = (await context.Merchants
                .AsNoTracking()
                .Where(m => m.IsActive)
                .Select(m => new { m.Id, m.MerchantCategoryId })
                .ToListAsync(cancellationToken))
            .GroupBy(m => m.MerchantCategoryId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.Id).OrderBy(id => id).ToList());

        var horizonMonths = MonthsSpanned(now, horizonEnd);

        var seasonalWeights = (await context.SeasonalPatterns
                .AsNoTracking()
                .Where(p => horizonMonths.Contains(p.Month))
                .Select(p => new { p.MerchantCategoryId, p.Month, p.Weight })
                .ToListAsync(cancellationToken))
            .ToDictionary(p => (p.MerchantCategoryId, p.Month), p => (double)p.Weight);

        // Which open or upcoming campaigns already single out a merchant in each category. A
        // campaign with no merchant criteria at all is horizontal, not category-targeted, so
        // it is deliberately not counted as coverage here.
        var coveringCampaigns = (await context.CampaignMerchants
                .AsNoTracking()
                .Where(cm => cm.Campaign.IsActive && cm.Campaign.Status != CampaignStatus.Ended)
                .Select(cm => new { cm.CampaignId, cm.Merchant.MerchantCategoryId })
                .Distinct()
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.MerchantCategoryId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.CampaignId).OrderBy(id => id).ToList());

        var maxNetSpend = aggregates.Max(a => a.NetSpend);

        if (maxNetSpend <= 0m)
        {
            return [];
        }

        var scored = new List<CampaignSuggestionDto>();

        foreach (var aggregate in aggregates)
        {
            if (aggregate.NetSpend < minimumSpend)
            {
                continue;
            }

            var covering = coveringCampaigns.GetValueOrDefault(aggregate.CategoryId, []);
            var isCoverageGap = covering.Count == 0;

            if (!isCoverageGap && !query.IncludeCovered)
            {
                continue;
            }

            var trendRatio = aggregate.PriorSpend > 0m
                ? (double)((aggregate.RecentSpend - aggregate.PriorSpend) / aggregate.PriorSpend)
                : (double?)null;

            var seasonalWeight = horizonMonths
                .Select(month => seasonalWeights.GetValueOrDefault((aggregate.CategoryId, month), 1.0))
                .DefaultIfEmpty(1.0)
                .Average();

            var normalisedSpend = (double)(aggregate.NetSpend / maxNetSpend);
            var clampedTrend = Math.Clamp(trendRatio ?? 0.0, -1.0, 3.0);

            var rawScore =
                _options.SpendWeight * normalisedSpend
                + _options.TrendWeight * clampedTrend
                + _options.SeasonWeight * (seasonalWeight - 1.0);

            var score = Math.Max(rawScore, 0.01)
                        * (isCoverageGap ? _options.CoverageGapBoost : 1.0);

            var categoryName = categoryNames.GetValueOrDefault(aggregate.CategoryId, $"#{aggregate.CategoryId}");
            var averageTicket = aggregate.PurchaseCount > 0
                ? aggregate.PurchaseSpend / aggregate.PurchaseCount
                : 0m;
            var suggestedReward = Math.Max(1m, Math.Round(averageTicket * (decimal)_options.SuggestedRewardRate, 0));

            scored.Add(new CampaignSuggestionDto
            {
                Score = Math.Round(score, 4),
                MerchantCategoryId = aggregate.CategoryId,
                MerchantCategoryName = categoryName,
                Headline = BuildHeadline(
                    categoryName, lookbackDays, aggregate.NetSpend, trendRatio, seasonalWeight,
                    isCoverageGap, covering.Count),
                Reason = new SuggestionReasonDto
                {
                    TotalSpend = aggregate.NetSpend,
                    TransactionCount = aggregate.PurchaseCount,
                    TrendRatio = trendRatio is null ? null : Math.Round(trendRatio.Value, 4),
                    SeasonalWeight = Math.Round(seasonalWeight, 4),
                    SeasonalMonths = horizonMonths,
                    IsCoverageGap = isCoverageGap,
                    CoveringCampaignIds = covering
                },
                Draft = new SuggestionDraftDto
                {
                    Name = $"{categoryName} kampanyası",
                    StartDate = now.Date,
                    EndDate = horizonEnd.Date,
                    SuggestedRewardPoint = suggestedReward,
                    MerchantCategoryId = aggregate.CategoryId,
                    MerchantIds = activeMerchantsByCategory.GetValueOrDefault(aggregate.CategoryId, [])
                }
            });
        }

        var ranked = scored
            .OrderByDescending(s => s.Score)
            .Take(maxSuggestions)
            .ToList();

        for (var i = 0; i < ranked.Count; i++)
        {
            ranked[i].Rank = i + 1;
        }

        return ranked;
    }

    /// <summary>The distinct calendar months, 1-12, that the interval [from, to] touches.</summary>
    private static List<int> MonthsSpanned(DateTime from, DateTime to)
    {
        var months = new List<int>();
        var cursor = new DateTime(from.Year, from.Month, 1);

        while (cursor <= to)
        {
            months.Add(cursor.Month);
            cursor = cursor.AddMonths(1);
        }

        return months.Distinct().ToList();
    }

    private static string BuildHeadline(
        string categoryName,
        int lookbackDays,
        decimal netSpend,
        double? trendRatio,
        double seasonalWeight,
        bool isCoverageGap,
        int coveringCount)
    {
        var sentence = $"{categoryName} kategorisinde son {lookbackDays} günde {netSpend:N0} ₺ harcama";

        if (trendRatio is >= 0.15)
        {
            sentence += $", harcama %{Math.Round(trendRatio.Value * 100)} arttı";
        }
        else if (trendRatio is <= -0.15)
        {
            sentence += $", harcama %{Math.Round(Math.Abs(trendRatio.Value) * 100)} azaldı";
        }

        if (seasonalWeight >= 1.1)
        {
            sentence += ", önümüzdeki dönem sezonsal olarak yüksek";
        }
        else if (seasonalWeight <= 0.9)
        {
            sentence += ", önümüzdeki dönem sezonsal olarak düşük";
        }

        sentence += isCoverageGap
            ? " — bu kategoride aktif kampanya yok."
            : $" — {coveringCount} aktif/yaklaşan kampanya zaten kapsıyor.";

        return sentence;
    }

    /// <summary>Per-category totals read from the transaction table.</summary>
    private sealed class CategoryAggregate
    {
        public int CategoryId { get; init; }

        /// <summary>Net spend (refund rows are negative) in the recent half of the window.</summary>
        public decimal RecentSpend { get; init; }

        /// <summary>Net spend in the earlier half of the window.</summary>
        public decimal PriorSpend { get; init; }

        /// <summary>Spend on purchase rows only, before refunds — sizes the suggested reward.</summary>
        public decimal PurchaseSpend { get; init; }

        /// <summary>Purchase-row count.</summary>
        public int PurchaseCount { get; init; }

        public decimal NetSpend => RecentSpend + PriorSpend;
    }
}
