using System.ComponentModel.DataAnnotations;

namespace CampaignSystem.DTOs;

public class CreateMerchantDto
{
    /// <summary>BKM merchant identifier. Unique.</summary>
    [Required]
    [MaxLength(20)]
    public string MerchantNumber { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string MerchantName { get; set; } = null!;
}
