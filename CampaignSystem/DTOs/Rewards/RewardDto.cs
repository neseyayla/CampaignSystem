namespace CampaignSystem.DTOs;

/// <summary>A reward row written by the batch. Maps to CAMPAIGN_REWARD.</summary>
public class RewardDto
{
    public long Id { get; set; }

    public int CampaignId { get; set; }

    public int CustomerId { get; set; }

    /// <summary>Null on a customer level reward, where all the customer's cards were pooled.</summary>
    public int? CardId { get; set; }

    public int QualifyingCount { get; set; }

    public decimal RewardPoint { get; set; }

    public DateTime RewardDate { get; set; }
}
