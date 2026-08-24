namespace CampaignSystem.DTOs;

/// <summary>
/// A campaign's rewards totalled per customer.
///
/// CAMPAIGN_REWARD holds one row per accumulation unit, so a card based campaign spreads a
/// single customer's reward across several rows. This rolls them up, which is what anyone
/// asking "what did this customer get from the campaign" actually wants.
/// </summary>
public class CampaignRewardSummaryDto
{
    public int CampaignId { get; set; }

    public string CampaignName { get; set; } = null!;

    /// <summary>How many distinct customers earned something.</summary>
    public int CustomerCount { get; set; }

    public decimal TotalRewardPoint { get; set; }

    public List<CampaignRewardCustomerLineDto> Customers { get; set; } = [];
}

public class CampaignRewardCustomerLineDto
{
    public int CustomerId { get; set; }

    public string CustomerNumber { get; set; } = null!;

    /// <summary>Rows in CAMPAIGN_REWARD — more than one only on a card based campaign.</summary>
    public int RewardRows { get; set; }

    public int QualifyingCount { get; set; }

    public decimal TotalRewardPoint { get; set; }
}
