using CampaignSystem.DTOs;
using CampaignSystem.Extensions;
using CampaignSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignSystem.Controllers;

/// <summary>
/// Everything a signed-in customer may ask about themselves.
///
/// No route here names a customer. The id comes from the token on every call, so there is no
/// parameter to change and no way to ask about somebody else — which is why these endpoints
/// replaced the earlier /api/customers/{id}/... ones rather than sitting alongside them.
///
/// The staff endpoints under /api/customers stay as they are: a call centre answering a
/// customer on the phone genuinely does need to look up another person's record.
/// </summary>
[ApiController]
[Authorize]
[Route("api/me")]
public class MeController(
    ICustomerCampaignService campaignService,
    IRewardService rewardService,
    ICardService cardService,
    ICustomerService customerService,
    ITransactionService transactionService,
    IAuthService authService) : ControllerBase
{
    /// <summary>The signed-in customer's own details.</summary>
    [HttpGet("profile")]
    [ProducesResponseType<CustomerProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerProfileDto>> GetProfile(CancellationToken cancellationToken)
    {
        var profile = await customerService.GetProfileAsync(User.CustomerId(), cancellationToken);

        return profile is null ? NotFound() : Ok(profile);
    }

    /// <summary>
    /// Changes the customer's own password. The current one must be given, not just a valid
    /// token, so a screen left unlocked cannot be used to lock its owner out.
    /// </summary>
    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordDto dto,
        CancellationToken cancellationToken)
    {
        var result = await authService.ChangePasswordAsync(
            User.CustomerId(), dto.CurrentPassword, dto.NewPassword, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => NoContent(),
            ResultStatus.NotFound => NotFound(),
            ResultStatus.Invalid => BadRequest(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>
    /// The running and upcoming campaigns this customer could earn from. Campaigns whose
    /// criteria rule them out never appear.
    /// </summary>
    [HttpGet("campaigns")]
    [ProducesResponseType<List<CustomerCampaignDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CustomerCampaignDto>>> GetCampaigns(
        CancellationToken cancellationToken)
    {
        var campaigns = await campaignService.GetEligibleAsync(User.CustomerId(), cancellationToken);

        return campaigns is null ? NotFound() : Ok(campaigns);
    }

    /// <summary>
    /// One campaign with this customer's standing in it.
    ///
    /// A campaign they are not eligible for answers 404, exactly as a campaign that does not
    /// exist does. Any other answer would disclose what is running for other people.
    /// </summary>
    [HttpGet("campaigns/{campaignId:int}")]
    [ProducesResponseType<CustomerCampaignDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerCampaignDetailDto>> GetCampaign(
        int campaignId,
        CancellationToken cancellationToken)
    {
        var result = await campaignService.GetOneAsync(User.CustomerId(), campaignId, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => Ok(result.Value),
            ResultStatus.NotFound => NotFound(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>
    /// Signs the customer up for a campaign that requires it.
    ///
    /// There is no matching withdrawal. An enrollment is only ever ended by the bank, when
    /// the customer stops meeting the campaign's criteria.
    /// </summary>
    [HttpPost("campaigns/{campaignId:int}/enrollment")]
    [ProducesResponseType<ParticipationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ParticipationDto>> Enroll(
        int campaignId,
        CustomerEnrollmentDto dto,
        CancellationToken cancellationToken)
    {
        var result = await campaignService.EnrollAsync(
            User.CustomerId(), campaignId, dto.CardId, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => CreatedAtAction(
                nameof(GetCampaign), new { campaignId }, result.Value),
            ResultStatus.NotFound => NotFound(),
            ResultStatus.Invalid => BadRequest(result.Error),
            ResultStatus.Conflict => Conflict(result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>What the customer has been paid, grouped by campaign.</summary>
    [HttpGet("rewards")]
    [ProducesResponseType<CustomerRewardSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerRewardSummaryDto>> GetRewards(
        CancellationToken cancellationToken)
    {
        var summary = await rewardService.GetCustomerSummaryAsync(User.CustomerId(), cancellationToken);

        return summary is null ? NotFound() : Ok(summary);
    }

    /// <summary>
    /// The purchases behind one campaign's reward, for the drill-down under "Kazandıklarım":
    /// each earning purchase, and any refunded one flagged so the screen can show it in red.
    /// </summary>
    [HttpGet("rewards/{campaignId:int}/transactions")]
    [ProducesResponseType<RewardBreakdownDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RewardBreakdownDto>> GetRewardBreakdown(
        int campaignId,
        CancellationToken cancellationToken)
    {
        var breakdown = await rewardService.GetRewardBreakdownAsync(
            User.CustomerId(), campaignId, cancellationToken);

        return breakdown is null ? NotFound() : Ok(breakdown);
    }

    /// <summary>
    /// The customer's own cards, so they can pick one when joining a card based campaign.
    /// </summary>
    [HttpGet("cards")]
    [ProducesResponseType<List<CardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CardDto>>> GetCards(CancellationToken cancellationToken)
    {
        return Ok(await cardService.GetAllAsync(User.CustomerId(), cancellationToken));
    }

    /// <summary>The customer's own spending history, newest first, for the profile screen.</summary>
    [HttpGet("transactions")]
    [ProducesResponseType<List<CustomerTransactionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CustomerTransactionDto>>> GetTransactions(
        CancellationToken cancellationToken)
    {
        return Ok(await transactionService.GetCustomerHistoryAsync(User.CustomerId(), cancellationToken));
    }
}
