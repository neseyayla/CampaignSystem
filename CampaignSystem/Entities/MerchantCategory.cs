namespace CampaignSystem.Entities;

/// <summary>
/// Sector a merchant belongs to (Akaryakıt, Giyim, Elektronik, ...). Maps to MERCHANT_CATEGORY.
/// </summary>
public class MerchantCategory
{
    public int Id { get; set; }

    public string CategoryCode { get; set; } = null!;

    public string CategoryName { get; set; } = null!;

    public ICollection<Merchant> Merchants { get; set; } = [];

    /// <summary>Per-month seasonal weights for this category. Empty means every month is average.</summary>
    public ICollection<SeasonalPattern> SeasonalPatterns { get; set; } = [];
}
