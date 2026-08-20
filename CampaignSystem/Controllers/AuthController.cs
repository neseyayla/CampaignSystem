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

    /// <summary>
    /// Exchanges a staff member's customer number and password for an admin token.
    ///
    /// A separate endpoint from the customer login on purpose: it mints a token carrying the
    /// "Admin" role, and only for a row flagged as an admin, so the customer sign-in can never
    /// be a way onto the staff side. Every failure answers 400 with the same wording, for the
    /// same reason it does on the customer login.
    /// </summary>
    [HttpPost("admin/login")]
    [ProducesResponseType<LoginResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResultDto>> AdminLogin(
        LoginDto dto,
        CancellationToken cancellationToken)
    {
        var result = await authService.AdminLoginAsync(dto, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => Ok(result.Value),
            ResultStatus.Invalid => BadRequest(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
