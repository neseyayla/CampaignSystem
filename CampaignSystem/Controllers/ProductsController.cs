using CampaignSystem.DTOs;
using CampaignSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignSystem.Controllers;

// Reads are open to any signed-in user: a card holder needs product names to read their own
// cards, and product names are harmless reference data. Managing products stays Admin-only,
// pinned on each write below.
[ApiController]
[Authorize]
[Route("api/products")]
public class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<ProductDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProductDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await productService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var product = await productService.GetByIdAsync(id, cancellationToken);

        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> Create(
        CreateProductDto dto,
        CancellationToken cancellationToken)
    {
        var result = await productService.CreateAsync(dto, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => CreatedAtAction(
                nameof(GetById), new { id = result.Value!.Id }, result.Value),
            ResultStatus.Conflict => Conflict(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        UpdateProductDto dto,
        CancellationToken cancellationToken)
    {
        var result = await productService.UpdateAsync(id, dto, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => NoContent(),
            ResultStatus.NotFound => NotFound(),
            ResultStatus.Conflict => Conflict(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>Hard delete — Product carries no IsActive flag.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await productService.DeleteAsync(id, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => NoContent(),
            ResultStatus.NotFound => NotFound(),
            ResultStatus.Conflict => Conflict(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
