namespace CampaignSystem.Entities;

/// <summary>
/// A calendar prior for how much a merchant category's card spend swells or shrinks in a
/// given month. Maps to SEASONAL_PATTERN.
///
/// It is a hint, not a measurement: <see cref="Weight"/> 1.00 is an ordinary month, a value
/// above it a known seasonal peak (stationery in August and September, fuel across the
/// summer, electronics in November), a value below it a lull. The recommendation engine
/// multiplies a category's live spend trend by the weight for the months it is about to
/// suggest a campaign over, so "school spending climbs before term starts" turns into a
/// ranked suggestion even before this year's transactions show it.
///
/// A category with no row for a month is treated as 1.00. Only the months that actually
/// deviate from an average one are seeded.
/// </summary>
public class SeasonalPattern
{
    public int Id { get; set; }

    public int MerchantCategoryId { get; set; }

    /// <summary>Calendar month the weight applies to, 1 (January) to 12 (December).</summary>
    public int Month { get; set; }

    /// <summary>
    /// Multiplier on the category's spend for this month. 1.00 is a neutral month; 1.40 means
    /// spend runs about 40% above the yearly average, 0.80 about 20% below.
    /// </summary>
    public decimal Weight { get; set; }

    public MerchantCategory MerchantCategory { get; set; } = null!;
}
