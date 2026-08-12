namespace CampaignSystem.Entities;

/// <summary>
/// Restricts a campaign to one or more card products. Maps to CAMPAIGN_PRODUCT.
/// No rows for a campaign means every product qualifies.
/// </summary>
public class CampaignProduct
{
    public int CampaignId { get; set; }

    public int ProductId { get; set; }

    public Campaign Campaign { get; set; } = null!;

    public Product Product { get; set; } = null!;
}
