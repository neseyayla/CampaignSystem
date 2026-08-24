using CampaignSystem.Enums;

namespace CampaignSystem.DTOs;

/// <summary>
/// A card as the API returns it. The clear card number is never part of this — it is not
/// stored in any table (PCI DSS), so there is nothing to expose.
/// </summary>
public class CardDto
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public int ProductId { get; set; }

    public CardType? CardType { get; set; }

    public bool IsActive { get; set; }
}
