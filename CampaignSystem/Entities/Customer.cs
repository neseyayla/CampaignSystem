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

    /// <summary>
    /// PBKDF2 hash of the customer's password, salted per row.
    ///
    /// Null for a customer who has never been given one, and that is a refusal rather than
    /// a gap: sign-in compares against this and a null can match nothing. The clear password
    /// is never stored, never logged and never returned by any endpoint.
    /// </summary>
    public string? PasswordHash { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this row may sign in to the staff application. False for an ordinary card
    /// holder; the admin sign-in refuses anyone without it, so the customer application and
    /// the staff one draw on the same table without a customer ever reaching the staff side.
    /// </summary>
    public bool IsAdmin { get; set; }

    public Segment? Segment { get; set; }

    public ICollection<Card> Cards { get; set; } = [];

    public ICollection<Transaction> Transactions { get; set; } = [];

    public ICollection<CampaignParticipation> Participations { get; set; } = [];

    public ICollection<CampaignReward> Rewards { get; set; } = [];
}
