namespace CampaignSystem.DTOs;

/// <summary>
/// Everything one customer has earned, grouped by campaign. Answers the question a call
/// centre gets: "how many points have I been given, and which campaigns did they come from".
/// </summary>
public class CustomerRewardSummaryDto
{
    public int CustomerId { get; set; }

    public string CustomerNumber { get; set; } = null!;

    public decimal TotalRewardPoint { get; set; }

    public List<CustomerRewardCampaignLineDto> Campaigns { get; set; } = [];
}

public class CustomerRewardCampaignLineDto
{
    public int CampaignId { get; set; }

    public string CampaignName { get; set; } = null!;

    public int RewardRows { get; set; }

    public int QualifyingCount { get; set; }

    public decimal TotalRewardPoint { get; set; }

    /// <summary>The most recent RewardDate among the rows, i.e. when the points were granted.</summary>
    public DateTime RewardDate { get; set; }
}
