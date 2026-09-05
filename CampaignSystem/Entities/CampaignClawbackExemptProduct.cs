namespace CampaignSystem.Entities;

/// <summary>
/// A card product exempt from a campaign's unused-points clawback. Maps to
/// CAMPAIGN_CLAWBACK_EXEMPT_PRODUCT.
///
/// Distinct from <see cref="CampaignProduct"/>, which restricts a campaign's scope — this
/// table instead exempts a product from a rule that would otherwise apply campaign-wide. No
/// rows for a campaign means no product is exempt.
/// </summary>
public class CampaignClawbackExemptProduct
{
    public int CampaignId { get; set; }

    public int ProductId { get; set; }

    public Campaign Campaign { get; set; } = null!;

    public Product Product { get; set; } = null!;
}
