using CampaignSystem.DTOs;
using CampaignSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace CampaignSystem.Controllers;

[ApiController]
[Route("api/campaigns")]
public class CampaignsController(ICampaignService campaignService) : ControllerBase
{
    /// <summary>Lists the active campaigns.</summary>
    [HttpGet]
    [ProducesResponseType<List<CampaignDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CampaignDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await campaignService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<CampaignDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var campaign = await campaignService.GetByIdAsync(id, cancellationToken);

        return campaign is null ? NotFound() : Ok(campaign);
    }

    [HttpPost]
    [ProducesResponseType<CampaignDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CampaignDto>> Create(
        CreateCampaignDto dto,
        CancellationToken cancellationToken)
    {
        var created = await campaignService.CreateAsync(dto, cancellationToken);

        // 201 with a Location header pointing at the new resource.
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        UpdateCampaignDto dto,
        CancellationToken cancellationToken)
    {
        var updated = await campaignService.UpdateAsync(id, dto, cancellationToken);

        return updated ? NoContent() : NotFound();
    }

    /// <summary>Soft delete — the row is kept and IsActive is cleared.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await campaignService.DeleteAsync(id, cancellationToken);

        return deleted ? NoContent() : NotFound();
    }

    /// <summary>
    /// The campaign's scope. An empty list means the campaign is unrestricted on that
    /// dimension rather than that it matches nothing.
    /// </summary>
    [HttpGet("{id:int}/criteria")]
    [ProducesResponseType<CampaignCriteriaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignCriteriaDto>> GetCriteria(
        int id,
        CancellationToken cancellationToken)
    {
        var criteria = await campaignService.GetCriteriaAsync(id, cancellationToken);

        return criteria is null ? NotFound() : Ok(criteria);
    }

    /// <summary>
    /// Replaces the campaign's whole scope. Criteria left out of the request are removed,
    /// so sending the same body twice produces the same result.
    /// </summary>
    [HttpPut("{id:int}/criteria")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetCriteria(
        int id,
        CampaignCriteriaDto dto,
        CancellationToken cancellationToken)
    {
        var outcome = await campaignService.SetCriteriaAsync(id, dto, cancellationToken);

        return outcome.Status switch
        {
            SetCriteriaStatus.Success => NoContent(),
            SetCriteriaStatus.CampaignNotFound => NotFound(),
            SetCriteriaStatus.InvalidReference => BadRequest(outcome.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
