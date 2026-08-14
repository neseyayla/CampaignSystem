using CampaignSystem.DTOs;
using CampaignSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace CampaignSystem.Controllers;

[ApiController]
[Route("api/campaigns/{campaignId:int}/rewards")]
public class CampaignRewardsController(IRewardService rewardService) : ControllerBase
{
    /// <summary>The rewards already written for this campaign.</summary>
    [HttpGet]
    [ProducesResponseType<List<RewardDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<RewardDto>>> GetAll(
        int campaignId,
        CancellationToken cancellationToken)
    {
        var rewards = await rewardService.GetByCampaignAsync(campaignId, cancellationToken);

        return rewards is null ? NotFound() : Ok(rewards);
    }

    /// <summary>
    /// What the customer would earn if the campaign were evaluated right now. Writes
    /// nothing, so it can be called while the campaign is still running and as often as
    /// the customer looks.
    /// </summary>
    [HttpGet("preview")]
    [ProducesResponseType<RewardPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RewardPreviewDto>> Preview(
        int campaignId,
        [FromQuery] int customerId,
        CancellationToken cancellationToken)
    {
        var result = await rewardService.PreviewAsync(campaignId, customerId, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => Ok(result.Value),
            ResultStatus.NotFound => NotFound(),
            ResultStatus.Invalid => BadRequest(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>
    /// Runs the end-of-campaign batch and writes the rewards. Allowed once: a second call
    /// is refused rather than recalculated, because the first run's figures have already
    /// been given to customers.
    /// </summary>
    [HttpPost("calculate")]
    [ProducesResponseType<RewardCalculationResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RewardCalculationResultDto>> Calculate(
        int campaignId,
        CancellationToken cancellationToken)
    {
        var result = await rewardService.CalculateAsync(campaignId, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => Ok(result.Value),
            ResultStatus.NotFound => NotFound(),
            ResultStatus.Invalid => BadRequest(result.Error),
            ResultStatus.Conflict => Conflict(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
