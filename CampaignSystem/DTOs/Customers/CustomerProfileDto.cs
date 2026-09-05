using CampaignSystem.Enums;

namespace CampaignSystem.DTOs;

/// <summary>
/// The signed-in customer's own details, for their profile screen.
///
/// Carries no id and no password: the id lives in the token, and a password is never read
/// back from anywhere. Everything here is safe to show the customer about themselves.
/// </summary>
public class CustomerProfileDto
{
    public string CustomerNumber { get; set; } = null!;

    public Gender Gender { get; set; }

    /// <summary>Null when the customer is in no segment. Shown by name, not by id.</summary>
    public string? SegmentName { get; set; }

    public int CardCount { get; set; }
}
