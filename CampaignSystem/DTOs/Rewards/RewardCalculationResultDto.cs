namespace CampaignSystem.DTOs;

/// <summary>Summary of a completed batch run, so the caller can see what it did.</summary>
public class RewardCalculationResultDto
{
    public int CampaignId { get; set; }

    /// <summary>How many transactions met every criterion.</summary>
    public int QualifyingTransactions { get; set; }

    /// <summary>How many rows were written to CAMPAIGN_REWARD.</summary>
    public int RewardsCreated { get; set; }

    /// <summary>Total points granted across those rows.</summary>
    public decimal TotalRewardPoint { get; set; }

    public DateTime CalculatedAt { get; set; }
}
