using System.ComponentModel.DataAnnotations;
using CampaignSystem.Enums;

namespace CampaignSystem.DTOs;

public class CreateCardDto
{
    /// <summary>Must name an existing active customer.</summary>
    [Required]
    public int CustomerId { get; set; }

    /// <summary>Must name an existing card product.</summary>
    [Required]
    public int ProductId { get; set; }

    public CardType? CardType { get; set; }
}
