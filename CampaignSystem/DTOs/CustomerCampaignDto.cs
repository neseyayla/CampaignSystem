using CampaignSystem.Enums;

namespace CampaignSystem.DTOs;

/// <summary>
/// A campaign as the customer sees it.
///
/// Deliberately narrower than <see cref="CampaignDto"/>. The lifecycle status and the
/// active flag are the bank's bookkeeping and mean nothing to a card holder; what they
/// need is what the campaign pays, where it applies, and whether they still have to sign
/// up. The criteria appear as names rather than ids for the same reason.
/// </summary>
public class CustomerCampaignDto
{
    public int CampaignId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    /// <summary>
    /// False for a campaign that has not begun yet. Those are listed on purpose: an
    /// enrollment campaign can be signed up to before it starts, which is exactly when
    /// signing up is worth something.
    /// </summary>
    public bool HasStarted { get; set; }

    /// <summary>True when only the transactions of customers who signed up are counted.</summary>
    public bool EnrollmentRequired { get; set; }

    /// <summary>
    /// Whether this customer holds an active enrollment. Always false for a mass campaign,
    /// where there is nothing to enroll in and everyone eligible already takes part.
    /// </summary>
    public bool Enrolled { get; set; }

    /// <summary>
    /// The level the campaign accumulates at. The customer's screen needs it to know
    /// whether enrolling means picking a card or signing up as a person.
    /// </summary>
    public EarningType EarningType { get; set; }

    /// <summary>What a single qualifying transaction pays.</summary>
    public decimal? RewardPoint { get; set; }

    /// <summary>
    /// The ceiling on what the campaign pays — per card or per customer, whichever
    /// <see cref="EarningType"/> says. Null when nothing caps it.
    /// </summary>
    public decimal? MaxRewardAmount { get; set; }

    /// <summary>Null when a transaction of any size counts.</summary>
    public decimal? MinimumAmount { get; set; }

    /// <summary>Null when there is no upper limit on a qualifying transaction.</summary>
    public decimal? MaximumAmount { get; set; }

    /// <summary>Where the spending has to happen. Empty means anywhere.</summary>
    public List<string> Merchants { get; set; } = [];

    /// <summary>Which kinds of transaction count. Empty means all of them.</summary>
    public List<string> TransactionCodes { get; set; } = [];
}
