namespace CampaignSystem.Entities;

/// <summary>
/// Result row written by the end-of-campaign batch job. Maps to CAMPAIGN_REWARD.
/// A unique constraint on (CampaignId, CustomerId, CardId) prevents double rewards
/// at the database level if the batch job runs twice.
/// </summary>
public class CampaignReward
{
    public long Id { get; set; }

    public int CampaignId { get; set; }

    public int CustomerId { get; set; }

    /// <summary>Null for a customer level reward.</summary>
    public int? CardId { get; set; }

    /// <summary>Number of transactions that met every campaign criterion.</summary>
    public int QualifyingCount { get; set; }

    /// <summary>Points granted, after the campaign cap has been applied.</summary>
    public decimal RewardPoint { get; set; }

    public DateTime RewardDate { get; set; }

    public Campaign Campaign { get; set; } = null!;

    public Customer Customer { get; set; } = null!;

    public Card? Card { get; set; }
}
