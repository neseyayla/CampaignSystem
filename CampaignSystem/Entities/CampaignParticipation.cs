using CampaignSystem.Enums;

namespace CampaignSystem.Entities;

/// <summary>
/// Enrollment record for campaigns where <see cref="Enums.CampaignType.EnrollmentRequired"/>.
/// Maps to CAMPAIGN_PARTICIPATION.
/// Enrollment is the customer's intent; eligibility is the batch job's decision.
/// Eligibility is deliberately not stored here.
/// </summary>
public class CampaignParticipation
{
    public long Id { get; set; }

    public int CampaignId { get; set; }

    public int CustomerId { get; set; }

    /// <summary>Null for a customer level enrollment.</summary>
    public int? CardId { get; set; }

    public DateTime ParticipationDate { get; set; }

    public ParticipationStatus Status { get; set; }

    public Campaign Campaign { get; set; } = null!;

    public Customer Customer { get; set; } = null!;

    public Card? Card { get; set; }
}
