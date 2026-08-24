using CampaignSystem.Enums;

namespace CampaignSystem.DTOs;

public class ParticipationDto
{
    public long Id { get; set; }

    public int CampaignId { get; set; }

    public int CustomerId { get; set; }

    /// <summary>Null on a customer level enrollment, where all the customer's cards count together.</summary>
    public int? CardId { get; set; }

    public DateTime ParticipationDate { get; set; }

    public ParticipationStatus Status { get; set; }
}
