using CampaignSystem.DTOs;
using CampaignSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignSystem.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/customers")]
public class CustomersController(
    ICustomerService customerService,
    IAuthService authService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<CustomerDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CustomerDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await customerService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var customer = await customerService.GetByIdAsync(id, cancellationToken);

        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPost]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerDto>> Create(
        CreateCustomerDto dto,
        CancellationToken cancellationToken)
    {
        var result = await customerService.CreateAsync(dto, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => CreatedAtAction(
                nameof(GetById), new { id = result.Value!.Id }, result.Value),
            ResultStatus.Invalid => BadRequest(result.Error),
            ResultStatus.Conflict => Conflict(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        UpdateCustomerDto dto,
        CancellationToken cancellationToken)
    {
        var result = await customerService.UpdateAsync(id, dto, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => NoContent(),
            ResultStatus.NotFound => NotFound(),
            ResultStatus.Invalid => BadRequest(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>
    /// Gives the customer a password, or replaces the one they had.
    ///
    /// A branch action, not the customer's own: this is how somebody who has never signed in
    /// gets their first password, and how a forgotten one is reset. Only the hash is kept, so
    /// there is no endpoint that reads a password back — a lost one can only be replaced.
    ///
    /// Unauthenticated for now, like the rest of the staff endpoints. That is not acceptable
    /// beyond development: as it stands, anyone who can reach the API can set any customer's
    /// password and then sign in as them.
    /// </summary>
    [HttpPut("{id:int}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPassword(
        int id,
        SetPasswordDto dto,
        CancellationToken cancellationToken)
    {
        var result = await authService.SetPasswordAsync(id, dto.Password, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => NoContent(),
            ResultStatus.NotFound => NotFound(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>Soft delete — the row is kept and IsActive is cleared.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await customerService.DeleteAsync(id, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => NoContent(),
            ResultStatus.NotFound => NotFound(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
