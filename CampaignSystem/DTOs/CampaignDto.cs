using CampaignSystem.Enums;

namespace CampaignSystem.DTOs;

/// <summary>
/// A campaign as the API returns it. Carries no navigation properties, so it serialises
/// without the Campaign to CampaignSegment to Campaign cycle the entity has.
/// </summary>
public class CampaignDto
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public CampaignType CampaignType { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public decimal? MinimumAmount { get; set; }

    public decimal? MaximumAmount { get; set; }

    public decimal? RewardPoint { get; set; }

    public decimal? MaxRewardAmount { get; set; }

    public EarningType EarningType { get; set; }

    /// <summary>Null when the campaign is open to every gender.</summary>
    public Gender? Gender { get; set; }

    /// <summary>Null when the campaign covers both primary and supplementary cards.</summary>
    public CardType? CardType { get; set; }

    public CampaignStatus Status { get; set; }

    public bool IsActive { get; set; }
}
