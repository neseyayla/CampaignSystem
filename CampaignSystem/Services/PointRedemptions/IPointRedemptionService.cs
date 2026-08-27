using CampaignSystem.DTOs;

namespace CampaignSystem.Services;

public interface IPointRedemptionService
{
    /// <summary>
    /// Records that a customer redeemed points earned from a campaign. NotFound when the
    /// campaign, customer or card does not exist; Invalid when the card does not belong to
    /// the customer.
    /// </summary>
    Task<ServiceResult<PointRedemptionDto>> CreateAsync(
        int campaignId,
        CreatePointRedemptionDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every redemption recorded against a campaign, most recent first. Null when the campaign
    /// is not found.
    /// </summary>
    Task<List<PointRedemptionDto>?> GetByCampaignAsync(
        int campaignId,
        CancellationToken cancellationToken = default);
}
