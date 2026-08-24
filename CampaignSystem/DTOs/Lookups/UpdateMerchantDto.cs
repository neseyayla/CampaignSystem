using System.ComponentModel.DataAnnotations;

namespace CampaignSystem.DTOs;

/// <summary>
/// MerchantNumber is absent on purpose: it is the BKM merchant identifier and identifies
/// the merchant everywhere else, so it is not ours to rewrite.
/// </summary>
public class UpdateMerchantDto
{
    [Required]
    [MaxLength(200)]
    public string MerchantName { get; set; } = null!;
}
