namespace CampaignSystem.Entities;

/// <summary>
/// Customer group used to target campaigns. Maps to SEGMENT.
/// </summary>
public class Segment
{
    public int Id { get; set; }

    public string SegmentCode { get; set; } = null!;

    public string SegmentName { get; set; } = null!;

    public ICollection<Customer> Customers { get; set; } = [];

    public ICollection<CampaignSegment> CampaignSegments { get; set; } = [];
}
