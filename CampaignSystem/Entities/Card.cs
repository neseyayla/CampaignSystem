using CampaignSystem.Enums;

namespace CampaignSystem.Entities;

/// <summary>
/// Credit card belonging to a customer. Maps to CARD.
/// The clear card number is never stored (PCI DSS).
/// </summary>
public class Card
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public int ProductId { get; set; }

    public CardType? CardType { get; set; }

    public bool IsActive { get; set; } = true;

    public Customer Customer { get; set; } = null!;

    public Product Product { get; set; } = null!;

    public ICollection<Transaction> Transactions { get; set; } = [];

    public ICollection<CampaignParticipation> Participations { get; set; } = [];

    public ICollection<CampaignReward> Rewards { get; set; } = [];
}
