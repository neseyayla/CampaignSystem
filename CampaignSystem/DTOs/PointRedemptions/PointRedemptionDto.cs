namespace CampaignSystem.DTOs;

/// <summary>A recorded point redemption. Maps to POINT_REDEMPTION.</summary>
public class PointRedemptionDto
{
    public long Id { get; set; }

    public int CampaignId { get; set; }

    public int CustomerId { get; set; }

    /// <summary>Null for a customer level redemption.</summary>
    public int? CardId { get; set; }

    public decimal Amount { get; set; }

    public DateTime RedemptionDate { get; set; }

    public string? Note { get; set; }
}
