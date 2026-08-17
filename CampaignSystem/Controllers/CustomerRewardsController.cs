using CampaignSystem.DTOs;
using CampaignSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace CampaignSystem.Controllers;

[ApiController]
[Route("api/customers/{customerId:int}/rewards")]
public class CustomerRewardsController(IRewardService rewardService) : ControllerBase
{
    /// <summary>
    /// Everything the customer has earned, grouped by campaign. This is the view a call
    /// centre needs when a customer asks where their points came from.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<CustomerRewardSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerRewardSummaryDto>> GetSummary(
        int customerId,
        CancellationToken cancellationToken)
    {
        var summary = await rewardService.GetCustomerSummaryAsync(customerId, cancellationToken);

        return summary is null ? NotFound() : Ok(summary);
    }
}
