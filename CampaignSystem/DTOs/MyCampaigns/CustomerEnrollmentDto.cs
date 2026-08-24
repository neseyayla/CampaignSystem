namespace CampaignSystem.DTOs;

/// <summary>
/// The customer's request to join a campaign.
///
/// Carries no CustomerId: that comes from the route, and later from the token. Letting the
/// body name a customer would mean the two could disagree, and the safe reading of that
/// disagreement is not obvious.
/// </summary>
public class CustomerEnrollmentDto
{
    /// <summary>
    /// Which card to sign up. Required for a card based campaign, and must be left out for
    /// a customer based one, where every card is pooled anyway.
    /// </summary>
    public int? CardId { get; set; }
}
