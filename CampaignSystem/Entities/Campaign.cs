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

    /// <summary>
    /// Reward cap. Applied to each reward row, so it follows whatever unit the campaign
    /// accumulates in: per customer under <see cref="EarningType.CustomerBased"/>, per card
    /// under <see cref="EarningType.CardBased"/>.
    /// </summary>
    public decimal? MaxRewardAmount { get; set; }

    public EarningType EarningType { get; set; }

    /// <summary>
    /// The stored status. Only <see cref="CampaignStatus.Ended"/> is meaningful here: it
    /// records that the reward batch has run, which is the one thing that cannot be worked
    /// out from the dates. Read <see cref="CurrentStatus"/> instead of this.
    /// </summary>
    public CampaignStatus Status { get; set; }

    /// <summary>
    /// Where the campaign actually stands. The first three states follow from the dates, so
    /// storing them would only create a value that can go stale; the fourth is a fact about
    /// what the batch has done, and that is what <see cref="Status"/> keeps.
    /// </summary>
    public CampaignStatus CurrentStatus
    {
        get
        {
            if (Status == CampaignStatus.Ended)
            {
                return CampaignStatus.Ended;
            }

            var now = DateTime.Now;

            if (now < StartDate)
            {
                return CampaignStatus.Pending;
            }

            return now <= EndDate ? CampaignStatus.Ongoing : CampaignStatus.Loading;
        }
    }

    /// <summary>
    /// Whether transactions accumulate per card, which decides how many reward rows one
    /// customer receives. Kept here rather than in a service so the reward batch and the
    /// enrollment rules cannot read it differently.
    /// </summary>
    public bool AccumulatesPerCard => EarningType == EarningType.CardBased;

    public bool IsActive { get; set; } = true;

    public ICollection<CampaignSegment> CampaignSegments { get; set; } = [];

    public ICollection<CampaignProduct> CampaignProducts { get; set; } = [];

    public ICollection<CampaignMerchant> CampaignMerchants { get; set; } = [];

    public ICollection<CampaignTransactionCode> CampaignTransactionCodes { get; set; } = [];

    public ICollection<CampaignParticipation> Participations { get; set; } = [];

    public ICollection<CampaignReward> Rewards { get; set; } = [];
}
