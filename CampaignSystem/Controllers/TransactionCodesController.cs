using CampaignSystem.DTOs;
using CampaignSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace CampaignSystem.Controllers;

[ApiController]
[Route("api/transaction-codes")]
public class TransactionCodesController(ITransactionCodeService transactionCodeService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<TransactionCodeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TransactionCodeDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await transactionCodeService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<TransactionCodeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionCodeDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var transactionCode = await transactionCodeService.GetByIdAsync(id, cancellationToken);

        return transactionCode is null ? NotFound() : Ok(transactionCode);
    }

    [HttpPost]
    [ProducesResponseType<TransactionCodeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TransactionCodeDto>> Create(
        CreateTransactionCodeDto dto,
        CancellationToken cancellationToken)
    {
        var result = await transactionCodeService.CreateAsync(dto, cancellationToken);

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
        UpdateTransactionCodeDto dto,
        CancellationToken cancellationToken)
    {
        var result = await transactionCodeService.UpdateAsync(id, dto, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => NoContent(),
            ResultStatus.NotFound => NotFound(),
            ResultStatus.Conflict => Conflict(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>Hard delete — TransactionCode carries no IsActive flag.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await transactionCodeService.DeleteAsync(id, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => NoContent(),
            ResultStatus.NotFound => NotFound(),
            ResultStatus.Conflict => Conflict(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
