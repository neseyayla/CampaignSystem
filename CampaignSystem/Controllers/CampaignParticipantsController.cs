using CampaignSystem.DTOs;
using CampaignSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace CampaignSystem.Controllers;

/// <summary>
/// Enrollments always belong to a campaign, so they hang off the campaign's route rather
/// than living at a top level of their own.
/// </summary>
[ApiController]
[Route("api/campaigns/{campaignId:int}/participants")]
public class CampaignParticipantsController(IParticipationService participationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<ParticipationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ParticipationDto>>> GetAll(
        int campaignId,
        CancellationToken cancellationToken)
    {
        var participants = await participationService.GetByCampaignAsync(campaignId, cancellationToken);

        return participants is null ? NotFound() : Ok(participants);
    }

    [HttpPost]
    [ProducesResponseType<ParticipationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ParticipationDto>> Enroll(
        int campaignId,
        CreateParticipationDto dto,
        CancellationToken cancellationToken)
    {
        var result = await participationService.EnrollAsync(campaignId, dto, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => CreatedAtAction(
                nameof(GetAll), new { campaignId }, result.Value),
            ResultStatus.NotFound => NotFound(),
            ResultStatus.Invalid => BadRequest(result.Error),
            ResultStatus.Conflict => Conflict(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>
    /// Cancels an enrollment. Administrative: customers cannot withdraw from a campaign,
    /// so this is for invalidating an enrollment or correcting a mistake.
    /// </summary>
    [HttpDelete("{participationId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(
        int campaignId,
        long participationId,
        CancellationToken cancellationToken)
    {
        var result = await participationService.CancelAsync(campaignId, participationId, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => NoContent(),
            ResultStatus.NotFound => NotFound(),
            ResultStatus.Invalid => BadRequest(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
