using System.ComponentModel.DataAnnotations;

namespace CampaignSystem.DTOs;

/// <summary>What a caller sends to record that a customer redeemed campaign points.</summary>
public class CreatePointRedemptionDto
{
    [Required]
    public int CustomerId { get; set; }

    /// <summary>Null for a customer level redemption.</summary>
    public int? CardId { get; set; }

    [Required]
    [Range(0.01, 9999999999999999.99)]
    public decimal Amount { get; set; }

    [Required]
    public DateTime RedemptionDate { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}
