namespace CampaignSystem.Enums;

/// <summary>
/// Whether the campaign requires the customer to enroll.
/// Persisted as the string codes shown below.
/// </summary>
public enum CampaignType
{
    /// <summary>MASS — applies to every eligible customer, no enrollment needed.</summary>
    Mass = 1,

    /// <summary>SI — the customer must enroll before transactions count.</summary>
    EnrollmentRequired = 2
}
