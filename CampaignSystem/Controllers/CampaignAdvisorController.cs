using CampaignSystem.DTOs.Advisor;
using CampaignSystem.Services;
using CampaignSystem.Services.Advisor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignSystem.Controllers;

/// <summary>
/// Plain-language questions about campaign opportunities. See
/// <see cref="ICampaignAdvisorService"/> for what the advisor is allowed to answer with.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/campaign-advisor")]
public class CampaignAdvisorController(ICampaignAdvisorService advisor) : ControllerBase
{
    /// <summary>
    /// Asks the advisor a question and returns its answer together with every tool call behind
    /// it. Takes seconds rather than milliseconds: each answer is several model round trips.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<AdvisorAnswerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdvisorAnswerDto>> Ask(
        [FromBody] AdvisorQuestionDto request,
        CancellationToken cancellationToken)
    {
        var result = await advisor.AskAsync(request.Question, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => Ok(result.Value),
            _ => BadRequest(result.Error)
        };
    }
}
