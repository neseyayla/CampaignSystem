namespace CampaignSystem.Entities;

/// <summary>
/// Restricts a campaign to one or more customer segments. Maps to CAMPAIGN_SEGMENT.
/// No rows for a campaign means every segment qualifies.
/// </summary>
public class CampaignSegment
{
    public int CampaignId { get; set; }

    public int SegmentId { get; set; }

    public Campaign Campaign { get; set; } = null!;

    public Segment Segment { get; set; } = null!;
}
