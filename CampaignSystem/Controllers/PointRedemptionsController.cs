using CampaignSystem.DTOs;
using CampaignSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignSystem.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/campaigns/{campaignId:int}/redemptions")]
public class PointRedemptionsController(IPointRedemptionService pointRedemptionService) : ControllerBase
{
    /// <summary>Every redemption recorded against this campaign, most recent first.</summary>
    [HttpGet]
    [ProducesResponseType<List<PointRedemptionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<PointRedemptionDto>>> GetAll(
        int campaignId,
        CancellationToken cancellationToken)
    {
        var redemptions = await pointRedemptionService.GetByCampaignAsync(campaignId, cancellationToken);

        return redemptions is null ? NotFound() : Ok(redemptions);
    }

    /// <summary>
    /// Records that a customer redeemed points earned from this campaign — the offset the
    /// unused-points clawback subtracts from what the campaign paid.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<PointRedemptionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PointRedemptionDto>> Create(
        int campaignId,
        CreatePointRedemptionDto dto,
        CancellationToken cancellationToken)
    {
        var result = await pointRedemptionService.CreateAsync(campaignId, dto, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => CreatedAtAction(nameof(GetAll), new { campaignId }, result.Value),
            ResultStatus.NotFound => NotFound(result.Error),
            ResultStatus.Invalid => BadRequest(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
