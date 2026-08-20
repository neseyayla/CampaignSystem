using CampaignSystem.DTOs;
using CampaignSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace CampaignSystem.Controllers;

/// <summary>
/// Signing in. The only endpoint a customer may reach without a token.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>
    /// Exchanges a customer number and password for a token.
    ///
    /// Every failure answers 400 with the same wording. A different message or status for an
    /// unknown customer number would let anyone work out which numbers exist by trying them.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType<LoginResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResultDto>> Login(
        LoginDto dto,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(dto, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => Ok(result.Value),
            ResultStatus.Invalid => BadRequest(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
