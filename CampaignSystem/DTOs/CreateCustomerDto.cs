using System.ComponentModel.DataAnnotations;
using CampaignSystem.Enums;

namespace CampaignSystem.DTOs;

public class CreateCustomerDto
{
    /// <summary>Identifier the customer carries in the core banking system. Unique.</summary>
    [Required]
    [MaxLength(20)]
    public string CustomerNumber { get; set; } = null!;

    public Gender? Gender { get; set; }

    /// <summary>Must name an existing segment when given.</summary>
    public int? SegmentId { get; set; }
}
