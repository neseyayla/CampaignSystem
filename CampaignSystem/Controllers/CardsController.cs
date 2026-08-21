using CampaignSystem.DTOs;
using CampaignSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignSystem.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/cards")]
public class CardsController(ICardService cardService) : ControllerBase
{
    /// <summary>Active cards. Pass customerId to narrow the list to one customer.</summary>
    [HttpGet]
    [ProducesResponseType<List<CardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CardDto>>> GetAll(
        [FromQuery] int? customerId,
        CancellationToken cancellationToken)
    {
        return Ok(await cardService.GetAllAsync(customerId, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<CardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CardDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var card = await cardService.GetByIdAsync(id, cancellationToken);

        return card is null ? NotFound() : Ok(card);
    }

    [HttpPost]
    [ProducesResponseType<CardDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CardDto>> Create(
        CreateCardDto dto,
        CancellationToken cancellationToken)
    {
        var result = await cardService.CreateAsync(dto, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => CreatedAtAction(
                nameof(GetById), new { id = result.Value!.Id }, result.Value),
            ResultStatus.Invalid => BadRequest(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        UpdateCardDto dto,
        CancellationToken cancellationToken)
    {
        var result = await cardService.UpdateAsync(id, dto, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => NoContent(),
            ResultStatus.NotFound => NotFound(),
            ResultStatus.Invalid => BadRequest(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>Soft delete — the row is kept and IsActive is cleared.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await cardService.DeleteAsync(id, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => NoContent(),
            ResultStatus.NotFound => NotFound(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
