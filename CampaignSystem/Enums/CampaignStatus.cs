namespace CampaignSystem.Enums;

/// <summary>
/// Lifecycle of a campaign definition.
/// Only <see cref="Published"/> campaigns are picked up by the evaluation batch job.
/// </summary>
public enum CampaignStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Published = 4,
    Completed = 5,
    Cancelled = 6
}
