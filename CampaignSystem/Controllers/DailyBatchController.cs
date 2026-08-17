using CampaignSystem.DTOs;
using CampaignSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace CampaignSystem.Controllers;

[ApiController]
[Route("api/batch")]
public class DailyBatchController(IDailyBatchService dailyBatchService) : ControllerBase
{
    /// <summary>
    /// Runs the end-of-day job: starts campaigns that have begun, closes those that have
    /// finished, and loads the rewards of those whose loading day has come.
    ///
    /// Safe to call more than once in a day. Each step only picks up campaigns that have not
    /// had it applied yet, so a second run does nothing rather than paying twice.
    /// </summary>
    [HttpPost("run")]
    [ProducesResponseType<DailyBatchResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DailyBatchResultDto>> Run(CancellationToken cancellationToken)
    {
        return Ok(await dailyBatchService.RunAsync(cancellationToken));
    }
}
