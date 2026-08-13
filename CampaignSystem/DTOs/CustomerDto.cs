using CampaignSystem.Enums;

namespace CampaignSystem.DTOs;

public class CustomerDto
{
    public int Id { get; set; }

    public string CustomerNumber { get; set; } = null!;

    public Gender? Gender { get; set; }

    public int? SegmentId { get; set; }

    public bool IsActive { get; set; }
}
