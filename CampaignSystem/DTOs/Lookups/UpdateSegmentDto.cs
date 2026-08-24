using System.ComponentModel.DataAnnotations;

namespace CampaignSystem.DTOs;

public class UpdateSegmentDto
{
    [Required]
    [MaxLength(10)]
    public string SegmentCode { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string SegmentName { get; set; } = null!;
}
