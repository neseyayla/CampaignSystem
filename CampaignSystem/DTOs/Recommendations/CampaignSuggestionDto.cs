namespace CampaignSystem.DTOs;

/// <summary>
/// One ranked campaign idea: a merchant category whose recent card spend, its trend and the
/// season it is heading into together say a campaign there is worth defining — together with
/// a draft the operator can open the campaign form on.
/// </summary>
public class CampaignSuggestionDto
{
    /// <summary>1 for the strongest idea, ascending. The position in the returned list.</summary>
    public int Rank { get; set; }

    /// <summary>The blended heuristic score. Comparable within one response, not across them.</summary>
    public double Score { get; set; }

    public int MerchantCategoryId { get; set; }

    public string MerchantCategoryName { get; set; } = null!;

    /// <summary>A ready-to-read sentence naming why this category surfaced.</summary>
    public string Headline { get; set; } = null!;

    /// <summary>The figures behind <see cref="Headline"/>, for an operator who wants the detail.</summary>
    public SuggestionReasonDto Reason { get; set; } = null!;

    /// <summary>A partly filled campaign the "create" button opens the form on.</summary>
    public SuggestionDraftDto Draft { get; set; } = null!;
}

/// <summary>The measurements a <see cref="CampaignSuggestionDto"/> was scored from.</summary>
public class SuggestionReasonDto
{
    /// <summary>Total card spend in the category over the lookback window, refunds netted out.</summary>
    public decimal TotalSpend { get; set; }

    /// <summary>Transaction count over the same window.</summary>
    public int TransactionCount { get; set; }

    /// <summary>
    /// Spend in the recent half of the window against the half before it, as a fraction:
    /// 0.42 means it grew 42%, -0.10 means it shrank 10%. Null when the earlier half is empty.
    /// </summary>
    public double? TrendRatio { get; set; }

    /// <summary>
    /// Average seasonal weight over the suggested campaign's months. Above 1 means the
    /// category is heading into a stronger-than-usual stretch, below 1 a lull.
    /// </summary>
    public double SeasonalWeight { get; set; }

    /// <summary>The months whose seasonal weights were averaged, 1 (January) to 12 (December).</summary>
    public List<int> SeasonalMonths { get; set; } = [];

    /// <summary>
    /// True when no open or upcoming campaign already targets this category. These are the
    /// gaps the engine is really looking for.
    /// </summary>
    public bool IsCoverageGap { get; set; }

    /// <summary>Ids of the open or upcoming campaigns that already cover the category, if any.</summary>
    public List<int> CoveringCampaignIds { get; set; } = [];
}

/// <summary>
/// A campaign skeleton built from a suggestion. Only the fields the engine has an opinion on
/// are set; the operator fills in the rest on the form as usual.
/// </summary>
public class SuggestionDraftDto
{
    public string Name { get; set; } = null!;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    /// <summary>Suggested points per qualifying transaction, from the category's average ticket.</summary>
    public decimal SuggestedRewardPoint { get; set; }

    public int MerchantCategoryId { get; set; }

    /// <summary>Active merchants in the category — the campaign's merchant criteria, prefilled.</summary>
    public List<int> MerchantIds { get; set; } = [];
}
