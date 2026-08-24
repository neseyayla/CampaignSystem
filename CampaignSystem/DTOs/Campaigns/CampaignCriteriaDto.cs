namespace CampaignSystem.DTOs;

/// <summary>
/// The scope of a campaign, as the four criteria junction tables hold it.
///
/// An empty list means the campaign is unrestricted on that dimension — a campaign with
/// only MerchantIds filled in applies to every segment and every card product at those
/// merchants. This mirrors the database rule that no rows for a campaign means no filter.
///
/// The same shape is used for reading and for writing, because a criteria set has no
/// server-assigned fields: what you send back is exactly what you got.
/// </summary>
public class CampaignCriteriaDto
{
    public List<int> SegmentIds { get; set; } = [];

    public List<int> ProductIds { get; set; } = [];

    public List<int> MerchantIds { get; set; } = [];

    public List<int> TransactionCodeIds { get; set; } = [];
}
