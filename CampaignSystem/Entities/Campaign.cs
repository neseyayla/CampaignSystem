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

    /// <summary>
    /// Only meaningful when <see cref="CampaignType"/> is <see cref="Enums.CampaignType.EnrollmentRequired"/>
    /// — null for a MASS campaign, which has no enrollment to measure from.
    /// </summary>
    public EnrollmentBasis? EnrollmentBasis { get; set; }

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

    /// <summary>
    /// Whether refunding a counted purchase claws its points back after the campaign has paid.
    /// Off means a refund is ignored once the reward is granted.
    /// </summary>
    public bool RefundClawbackEnabled { get; set; }

    /// <summary>
    /// How many days after the reward is loaded a refund can still claw points back. Null while
    /// clawback is enabled means no time limit; ignored when clawback is off.
    /// </summary>
    public int? RefundClawbackDays { get; set; }

    public EarningType EarningType { get; set; }

    /// <summary>
    /// Restricts the campaign to one gender. Null means no restriction, which is the same
    /// rule the criteria junction tables follow: saying nothing includes everyone.
    /// </summary>
    public Gender? Gender { get; set; }

    /// <summary>
    /// Restricts the campaign to primary or to supplementary cards. Null covers both — the
    /// "Hepsi" option on the screen this replaces.
    /// </summary>
    public CardType? CardType { get; set; }

    /// <summary>
    /// Where the campaign stands: Pending, Ongoing, Loading or Ended.
    ///
    /// Stored rather than worked out from the dates on every read, so that a query, a report
    /// or anyone looking at the table sees the same answer the application does. Keeping it
    /// truthful is the daily batch's job — it advances campaigns whose start or end date has
    /// arrived, then loads the rewards of the ones that are due.
    /// </summary>
    public CampaignStatus Status { get; set; }

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

    public ICollection<CampaignCondition> Conditions { get; set; } = [];

    public ICollection<CampaignParticipation> Participations { get; set; } = [];

    public ICollection<CampaignReward> Rewards { get; set; } = [];
}
