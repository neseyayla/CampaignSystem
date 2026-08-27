namespace CampaignSystem.Entities;

/// <summary>
/// A record that a customer redeemed (spent) points earned from a campaign. Maps to
/// POINT_REDEMPTION.
///
/// The system has no automated way to observe a customer spending campaign points — unlike a
/// purchase, which arrives on <see cref="Transaction"/>, a redemption is entered by an operator
/// or an external integration. The unused-points clawback subtracts the sum of these rows from
/// what a campaign paid to find what is still sitting unredeemed.
/// </summary>
public class PointRedemption
{
    public long Id { get; set; }

    public int CampaignId { get; set; }

    public int CustomerId { get; set; }

    /// <summary>
    /// Null for a customer level redemption. Set only when it should offset a card level
    /// reward — the same distinction <see cref="CampaignReward.CardId"/> carries.
    /// </summary>
    public int? CardId { get; set; }

    /// <summary>Points redeemed. Always positive — the amount subtracted from what was earned.</summary>
    public decimal Amount { get; set; }

    public DateTime RedemptionDate { get; set; }

    public string? Note { get; set; }

    public Campaign Campaign { get; set; } = null!;

    public Customer Customer { get; set; } = null!;

    public Card? Card { get; set; }
}
