using CampaignSystem.DTOs;
using CampaignSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignSystem.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/segments")]
public class SegmentsController(ISegmentService segmentService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<SegmentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SegmentDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await segmentService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<SegmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SegmentDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var segment = await segmentService.GetByIdAsync(id, cancellationToken);

        return segment is null ? NotFound() : Ok(segment);
    }

    [HttpPost]
    [ProducesResponseType<SegmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SegmentDto>> Create(
        CreateSegmentDto dto,
        CancellationToken cancellationToken)
    {
        var result = await segmentService.CreateAsync(dto, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => CreatedAtAction(
                nameof(GetById), new { id = result.Value!.Id }, result.Value),
            ResultStatus.Conflict => Conflict(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        UpdateSegmentDto dto,
        CancellationToken cancellationToken)
    {
        var result = await segmentService.UpdateAsync(id, dto, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => NoContent(),
            ResultStatus.NotFound => NotFound(),
            ResultStatus.Conflict => Conflict(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>Hard delete — Segment carries no IsActive flag.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await segmentService.DeleteAsync(id, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => NoContent(),
            ResultStatus.NotFound => NotFound(),
            ResultStatus.Conflict => Conflict(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
