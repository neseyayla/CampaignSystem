using CampaignSystem.Configuration;
using CampaignSystem.Data;
using CampaignSystem.DTOs.Advisor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CampaignSystem.Services.Advisor;

/// <summary>
/// Segment × category breakdown behind <see cref="ISegmentAnalysisService"/>.
///
/// Spend is defined once, here, and narrowly: sales (SA) netted against their refunds (IA),
/// at a merchant, inside the window. Cash advances and debt payments also move money on a
/// card, and summing every row of the transaction table would count them as spend — the
/// mistake a hand-written aggregate makes without failing, so it is closed off in one place.
///
/// A category the segment's customers barely touch gets its ratios withheld. With a handful
/// of buyers a share or an index is noise, and a cell that small starts describing
/// individual customers rather than a segment. The threshold is
/// <see cref="AdvisorOptions.SegmentMinCellCustomers"/>.
///
/// Works against the context directly, like the recommendation engine: the figures come from
/// grouping the transaction table, which is not what the generic repository is for.
/// </summary>
public class SegmentAnalysisService(
    CampaignDbContext context,
    IOptions<AdvisorOptions> options) : ISegmentAnalysisService
{
    private const string SaleCode = "SA";
    private const string RefundCode = "IA";
    private const int DefaultLookbackDays = 90;

    private readonly AdvisorOptions _options = options.Value;

    public async Task<ServiceResult<SegmentBreakdownDto>> GetCategoryBreakdownAsync(
        int segmentId,
        int? lookbackDays = null,
        CancellationToken cancellationToken = default)
    {
        var segment = await context.Segments
            .AsNoTracking()
            .Where(s => s.Id == segmentId)
            .Select(s => new { s.SegmentName, ActiveCustomers = s.Customers.Count(c => c.IsActive) })
            .FirstOrDefaultAsync(cancellationToken);

        if (segment is null)
        {
            return ServiceResult<SegmentBreakdownDto>.NotFound($"Segment {segmentId} does not exist.");
        }

        var days = Math.Clamp(lookbackDays ?? DefaultLookbackDays, 14, 365);
        var now = DateTime.Now;
        var windowStart = now.AddDays(-days);
        var midPoint = now.AddDays(-days / 2.0);

        var spending = context.Transactions
            .AsNoTracking()
            .Where(t => t.MerchantId != null
                        && t.TransactionDate >= windowStart
                        && t.TransactionDate < now
                        && (t.TransactionCode.Code == SaleCode || t.TransactionCode.Code == RefundCode))
            .Select(t => new
            {
                t.CustomerId,
                t.Customer.SegmentId,
                CategoryId = t.Merchant!.MerchantCategoryId,
                t.Amount,
                IsSale = t.TransactionCode.Code == SaleCode,
                IsRecent = t.TransactionDate >= midPoint
            });

        // Every segment's cells, not just this one's: the bank-wide share each index is
        // measured against comes out of the same grouping.
        var cells = await spending
            .GroupBy(x => new { x.SegmentId, x.CategoryId })
            .Select(g => new
            {
                g.Key.SegmentId,
                g.Key.CategoryId,
                NetSpend = g.Sum(x => x.Amount),
                RecentSpend = g.Sum(x => x.IsRecent ? x.Amount : 0m),
                SaleSpend = g.Sum(x => x.IsSale ? x.Amount : 0m),
                SaleCount = g.Sum(x => x.IsSale ? 1 : 0)
            })
            .ToListAsync(cancellationToken);

        // Distinct (category, customer) pairs first, then count — the same shape the report
        // uses, because a Distinct().Count() nested inside the grouping above does not translate.
        var buyers = await spending
            .Where(x => x.SegmentId == segmentId && x.IsSale)
            .Select(x => new { x.CategoryId, x.CustomerId })
            .Distinct()
            .GroupBy(x => x.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, cancellationToken);

        var spendingCustomers = await spending
            .Where(x => x.SegmentId == segmentId && x.IsSale)
            .Select(x => x.CustomerId)
            .Distinct()
            .CountAsync(cancellationToken);

        var categoryNames = await context.MerchantCategories
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.CategoryName, cancellationToken);

        var bankByCategory = cells
            .GroupBy(c => c.CategoryId)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.NetSpend));
        var bankTotal = bankByCategory.Values.Sum();
        var segmentCells = cells.Where(c => c.SegmentId == segmentId).ToDictionary(c => c.CategoryId);
        var segmentTotal = segmentCells.Values.Sum(c => c.NetSpend);

        var categories = bankByCategory.Keys
            .Select(categoryId =>
            {
                var name = categoryNames.GetValueOrDefault(categoryId, $"#{categoryId}");
                var buyerCount = buyers.GetValueOrDefault(categoryId);
                var shareOfBank = bankTotal > 0m ? (double)(bankByCategory[categoryId] / bankTotal) : (double?)null;

                if (!segmentCells.TryGetValue(categoryId, out var cell)
                    || buyerCount < _options.SegmentMinCellCustomers)
                {
                    return new SegmentCategoryDto(
                        categoryId, name, null, null, buyerCount, null, Round(shareOfBank),
                        null, null, null, null, EnoughData: false);
                }

                var shareOfSegment = segmentTotal > 0m ? (double)(cell.NetSpend / segmentTotal) : (double?)null;
                var index = shareOfSegment is not null && shareOfBank > 0 ? shareOfSegment / shareOfBank : null;
                var priorSpend = cell.NetSpend - cell.RecentSpend;

                return new SegmentCategoryDto(
                    categoryId,
                    name,
                    Math.Round(cell.NetSpend, 2),
                    cell.SaleCount,
                    buyerCount,
                    Round(shareOfSegment),
                    Round(shareOfBank),
                    Round(index),
                    Round(segment.ActiveCustomers > 0 ? buyerCount / (double)segment.ActiveCustomers : null),
                    cell.SaleCount > 0 ? Math.Round(cell.SaleSpend / cell.SaleCount, 2) : null,
                    Round(priorSpend > 0m ? (double)((cell.RecentSpend - priorSpend) / priorSpend) : null),
                    EnoughData: true);
            })
            .OrderByDescending(c => c.EnoughData)
            .ThenByDescending(c => c.Index ?? 0)
            .ToList();

        var months = (decimal)(days / 30.44);

        return ServiceResult<SegmentBreakdownDto>.Success(new SegmentBreakdownDto(
            segmentId,
            segment.SegmentName,
            segment.ActiveCustomers,
            spendingCustomers,
            Math.Round(segmentTotal, 2),
            segment.ActiveCustomers > 0 ? Math.Round(segmentTotal / segment.ActiveCustomers / months, 2) : 0m,
            days,
            _options.SegmentMinCellCustomers,
            categories));
    }

    private static double? Round(double? value) => value is null ? null : Math.Round(value.Value, 4);
}
