namespace CampaignSystem.Enums;

/// <summary>
/// State of a customer's enrollment in a campaign.
/// Only <see cref="Active"/> enrollments make a customer eligible.
/// </summary>
public enum ParticipationStatus
{
    Active = 1,
    Cancelled = 2
}
