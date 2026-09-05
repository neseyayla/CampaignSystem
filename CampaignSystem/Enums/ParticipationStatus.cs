namespace CampaignSystem.Enums;

/// <summary>
/// State of a customer's enrollment in a campaign.
/// Only <see cref="Active"/> enrollments are considered when rewards are calculated.
///
/// Persisted as the member name.
/// </summary>
public enum ParticipationStatus
{
    /// <summary>The enrollment stands.</summary>
    Active = 1,

    /// <summary>
    /// The enrollment was invalidated because the customer no longer meets the campaign's
    /// criteria — a changed segment, a closed card. A customer cannot withdraw from a
    /// campaign, so this is always the system's doing, never the customer's.
    /// </summary>
    Cancelled = 2
}
