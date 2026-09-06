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

    /// <summary>Aggregated statistics for a campaign, looked up by its id.</summary>
    [HttpGet("campaign/{id:int}")]
    public async Task<IActionResult> GetCampaignReport([FromRoute] int id, CancellationToken cancellationToken)
    {
        var report = await _service.GetCampaignReportByIdAsync(id, cancellationToken);

        return report is null ? NotFound() : Ok(report);
    }
}
