using System.ComponentModel.DataAnnotations;

namespace CampaignSystem.DTOs;

public class CreateTransactionCodeDto
{
    [Required]
    [MaxLength(10)]
    public string Code { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = null!;
}
