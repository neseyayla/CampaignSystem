using System.ComponentModel.DataAnnotations;

namespace CampaignSystem.DTOs;

/// <summary>
/// Enrolls a customer in a campaign.
///
/// Whether CardId is required follows from the campaign's EarningType, not from the
/// caller's preference: a card level campaign accumulates per card and needs to know
/// which one, a customer level campaign pools every card and must leave it empty.
/// </summary>
public class CreateParticipationDto
{
    [Required]
    public int CustomerId { get; set; }

    /// <summary>Required for a CardBased campaign, must be omitted for a CustomerBased one.</summary>
    public int? CardId { get; set; }
}
