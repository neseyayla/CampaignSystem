using CampaignSystem.Enums;

namespace CampaignSystem.Entities;

/// <summary>
/// Campaign definition. Maps to CAMPAIGN.
/// The scope of a campaign is never hardcoded — it is read from the four criteria
/// junction tables. An empty junction table means the campaign is unrestricted on
/// that dimension.
/// </summary>
public class Campaign
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public CampaignType CampaignType { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    /// <summary>Lower bound a single transaction must reach to qualify.</summary>
    public decimal? MinimumAmount { get; set; }

    /// <summary>Upper bound a single transaction must stay under to qualify.</summary>
    public decimal? MaximumAmount { get; set; }

    /// <summary>Points earned per qualifying transaction.</summary>
    public decimal? RewardPoint { get; set; }

    /// <summary>Reward cap for the whole campaign.</summary>
    public decimal? MaxRewardAmount { get; set; }

    public EarningType EarningType { get; set; }

    public CampaignStatus Status { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<CampaignSegment> CampaignSegments { get; set; } = [];

    public ICollection<CampaignProduct> CampaignProducts { get; set; } = [];

    public ICollection<CampaignMerchant> CampaignMerchants { get; set; } = [];

    public ICollection<CampaignTransactionCode> CampaignTransactionCodes { get; set; } = [];

    public ICollection<CampaignParticipation> Participations { get; set; } = [];

    public ICollection<CampaignReward> Rewards { get; set; } = [];
}
