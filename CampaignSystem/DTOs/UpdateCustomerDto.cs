using CampaignSystem.Enums;

namespace CampaignSystem.DTOs;

/// <summary>
/// CustomerNumber is absent on purpose: it comes from the core banking system and
/// identifies the customer everywhere else, so it is not ours to rewrite.
/// </summary>
public class UpdateCustomerDto
{
    public Gender? Gender { get; set; }

    public int? SegmentId { get; set; }
}
