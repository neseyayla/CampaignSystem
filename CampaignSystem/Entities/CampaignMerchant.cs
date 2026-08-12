namespace CampaignSystem.Entities;

/// <summary>
/// Restricts a campaign to one or more merchants. Maps to CAMPAIGN_MERCHANT.
/// No rows for a campaign means every merchant qualifies.
/// </summary>
public class CampaignMerchant
{
    public int CampaignId { get; set; }

    public int MerchantId { get; set; }

    public Campaign Campaign { get; set; } = null!;

    public Merchant Merchant { get; set; } = null!;
}
