namespace CampaignSystem.DTOs.Advisor;

/// <summary>
/// Where one segment's card spend goes, category by category, set against the whole bank.
/// Spend is sales net of their refunds over the lookback window; cash advances and debt
/// payments are not spend and are left out.
/// </summary>
public record SegmentBreakdownDto(
    int SegmentId,
    string SegmentName,
    int ActiveCustomers,
    /// <summary>Active or not, the segment's customers with at least one sale in the window.</summary>
    int SpendingCustomers,
    decimal NetSpend,
    /// <summary>Net spend per active customer per month, over the window.</summary>
    decimal MonthlySpendPerCustomer,
    int LookbackDays,
    /// <summary>Below this many buying customers a category's ratios are withheld.</summary>
    int MinCellCustomers,
    /// <summary>Most over-represented first; categories with too little data last.</summary>
    IReadOnlyList<SegmentCategoryDto> Categories);

/// <summary>
/// One merchant category in a segment breakdown. When <see cref="EnoughData"/> is false too
/// few of the segment's customers bought there: the segment's figures are withheld (null)
/// and only <see cref="Buyers"/> and the bank-wide share remain.
/// </summary>
public record SegmentCategoryDto(
    int MerchantCategoryId,
    string CategoryName,
    decimal? NetSpend,
    int? PurchaseCount,
    /// <summary>Distinct customers of the segment with a sale in the category.</summary>
    int Buyers,
    /// <summary>The category's share of the segment's spend, 0–1.</summary>
    double? ShareOfSegment,
    /// <summary>The category's share of the whole bank's spend, 0–1.</summary>
    double? ShareOfBank,
    /// <summary>ShareOfSegment / ShareOfBank: 2.0 is twice the bank's share, 0.5 half of it.</summary>
    double? Index,
    /// <summary>Buyers / the segment's active customers, 0–1.</summary>
    double? Penetration,
    decimal? AverageTicket,
    /// <summary>Recent half of the window against the half before it: 0.25 is +25%.</summary>
    double? TrendRatio,
    bool EnoughData);
