using CampaignSystem.Enums;

namespace CampaignSystem.Entities;

/// <summary>
/// Card holder. Maps to CUSTOMER.
/// </summary>
public class Customer
{
    public int Id { get; set; }

    public string CustomerNumber { get; set; } = null!;

    /// <summary>
    /// Recorded for every customer — the core banking system always supplies it, so a
    /// campaign that narrows on gender never has to decide what an unknown value means.
    /// </summary>
    public Gender Gender { get; set; }

    public int? SegmentId { get; set; }

    public bool IsActive { get; set; } = true;

    public Segment? Segment { get; set; }

    public ICollection<Card> Cards { get; set; } = [];

    public ICollection<Transaction> Transactions { get; set; } = [];

    public ICollection<CampaignParticipation> Participations { get; set; } = [];

    public ICollection<CampaignReward> Rewards { get; set; } = [];
}
