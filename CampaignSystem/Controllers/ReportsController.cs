using CampaignSystem.DTOs.Reports;
using CampaignSystem.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignSystem.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _service;

    public ReportsController(IReportService service)
    {
        _service = service;
    }

    /// <summary>Report rows for the campaigns matching the given filters, for the report table.</summary>
    [HttpGet("campaigns")]
    public async Task<IActionResult> GetCampaignSummaries(
        [FromQuery] int? campaignId,
        [FromQuery] ClawbackFilter clawback,
        CancellationToken cancellationToken)
    {
        var summaries = await _service.GetCampaignSummariesAsync(campaignId, clawback, cancellationToken);

        return Ok(summaries);
    }

    /// <summary>One campaign's movement ledger (loads, refunds, clawbacks), for the drill-down.</summary>
    [HttpGet("campaigns/{id:int}/movements")]
    public async Task<IActionResult> GetCampaignMovements(
        [FromRoute] int id,
        [FromQuery] MovementFilter type,
        CancellationToken cancellationToken)
    {
        var movements = await _service.GetCampaignMovementsAsync(id, type, cancellationToken);

        return Ok(movements);
    }
}
