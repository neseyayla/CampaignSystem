using System.ComponentModel.DataAnnotations;
using CampaignSystem.Enums;

namespace CampaignSystem.DTOs;

/// <summary>
/// CustomerId is absent on purpose: a card cannot be moved to another customer. Its
/// transactions, enrollments and rewards all hang off that relationship, and rewriting it
/// would silently reassign the customer's history.
/// </summary>
public class UpdateCardDto
{
    [Required]
    public int ProductId { get; set; }

    public CardType? CardType { get; set; }
}
