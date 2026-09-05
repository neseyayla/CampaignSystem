namespace CampaignSystem.Entities;

/// <summary>
/// Card product such as Classic, Gold or Platinum. Maps to PRODUCT.
/// </summary>
public class Product
{
    public int Id { get; set; }

    public string ProductCode { get; set; } = null!;

    public string ProductName { get; set; } = null!;

    public ICollection<Card> Cards { get; set; } = [];

    public ICollection<CampaignProduct> CampaignProducts { get; set; } = [];

    public ICollection<CampaignClawbackExemptProduct> ClawbackExemptCampaigns { get; set; } = [];
}
