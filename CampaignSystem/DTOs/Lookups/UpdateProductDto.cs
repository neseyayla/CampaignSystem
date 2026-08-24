using System.ComponentModel.DataAnnotations;

namespace CampaignSystem.DTOs;

public class UpdateProductDto
{
    [Required]
    [MaxLength(10)]
    public string ProductCode { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string ProductName { get; set; } = null!;
}
