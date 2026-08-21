using CampaignSystem.DTOs;
using CampaignSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignSystem.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/transactions")]
public class TransactionsController(ITransactionService transactionService) : ControllerBase
{
    /// <summary>
    /// Transactions, narrowed by whichever filters are given. All are optional.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<List<TransactionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TransactionDto>>> GetAll(
        [FromQuery] int? cardId,
        [FromQuery] int? customerId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        return Ok(await transactionService.GetAllAsync(cardId, customerId, from, to, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<TransactionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var transaction = await transactionService.GetByIdAsync(id, cancellationToken);

        return transaction is null ? NotFound() : Ok(transaction);
    }

    /// <summary>
    /// Records a transaction arriving from the card system. There is no update or delete:
    /// a correction arrives as a reversing transaction, not as an edit.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<TransactionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TransactionDto>> Create(
        CreateTransactionDto dto,
        CancellationToken cancellationToken)
    {
        var result = await transactionService.CreateAsync(dto, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => CreatedAtAction(
                nameof(GetById), new { id = result.Value!.Id }, result.Value),
            ResultStatus.Invalid => BadRequest(result.Error),
            ResultStatus.Conflict => Conflict(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
