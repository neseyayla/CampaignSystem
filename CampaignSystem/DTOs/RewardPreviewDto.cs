namespace CampaignSystem.DTOs;

/// <summary>
/// What a customer would earn if the campaign were evaluated right now.
///
/// Nothing is written when this is produced. It answers "how am I doing" while the
/// campaign is still running, and is calculated by the same query the batch uses, so the
/// two cannot drift apart.
/// </summary>
public class RewardPreviewDto
{
    public int CampaignId { get; set; }

    public int CustomerId { get; set; }

    /// <summary>
    /// One entry per card for a card based campaign, a single entry with a null CardId for
    /// a customer based one.
    /// </summary>
    public List<RewardPreviewLineDto> Lines { get; set; } = [];

    /// <summary>Sum of the lines, after each has had the campaign cap applied.</summary>
    public decimal TotalRewardPoint { get; set; }
}

public class RewardPreviewLineDto
{
    /// <summary>Null when the campaign accumulates per customer.</summary>
    public int? CardId { get; set; }

    public int QualifyingCount { get; set; }

    /// <summary>QualifyingCount multiplied by the campaign's RewardPoint, before the cap.</summary>
    public decimal EarnedBeforeCap { get; set; }

    /// <summary>What would actually be granted, once MaxRewardAmount has been applied.</summary>
    public decimal RewardPoint { get; set; }

    /// <summary>True when the cap held the reward below what the transactions earned.</summary>
    public bool CapApplied { get; set; }
}
