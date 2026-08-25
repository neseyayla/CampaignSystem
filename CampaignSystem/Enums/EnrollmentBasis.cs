namespace CampaignSystem.Enums;

/// <summary>
/// For an <see cref="CampaignType.EnrollmentRequired"/> (SI) campaign, which transactions
/// count toward the reward once a customer has enrolled. Meaningless for a MASS campaign,
/// where there is no enrollment to measure from — the field stays null there.
/// </summary>
public enum EnrollmentBasis
{
    /// <summary>Only transactions from the customer's (or card's) enrollment date onward count.</summary>
    ParticipationDate = 1,

    /// <summary>
    /// Every transaction in the campaign's date range counts for an enrolled customer,
    /// regardless of when within that range they joined.
    /// </summary>
    CampaignPeriod = 2
}
