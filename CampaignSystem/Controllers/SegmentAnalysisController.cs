using CampaignSystem.DTOs.Advisor;
using CampaignSystem.Services;
using CampaignSystem.Services.Advisor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignSystem.Controllers;

/// <summary>
/// A segment's spend by merchant category, set against the bank. The same figures the AI
/// advisor reads, served directly — so they can be checked, or shown, without asking the
/// model anything. See <see cref="ISegmentAnalysisService"/>.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/segment-analysis")]
public class SegmentAnalysisController(ISegmentAnalysisService analysis) : ControllerBase
{
    /// <summary>
    /// The segment's categories, most over-represented first. <paramref name="lookbackDays"/>
    /// defaults to 90 and is clamped to 14–365.
    /// </summary>
    [HttpGet("{segmentId:int}")]
    [ProducesResponseType<SegmentBreakdownDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SegmentBreakdownDto>> Get(
        int segmentId,
        [FromQuery] int? lookbackDays,
        CancellationToken cancellationToken)
    {
        var result = await analysis.GetCategoryBreakdownAsync(segmentId, lookbackDays, cancellationToken);

        return result.Status == ResultStatus.Success
            ? Ok(result.Value)
            : NotFound(result.Error);
    }
}
