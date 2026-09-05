using CampaignSystem.DTOs;
using CampaignSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignSystem.Controllers;

/// <summary>
/// Read-only campaign ideas for an operator, ranked from recent transaction history. See
/// <see cref="ICampaignRecommendationService"/> for how the ranking is built.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/campaign-recommendations")]
public class CampaignRecommendationsController(ICampaignRecommendationService recommendationService)
    : ControllerBase
{
    /// <summary>
    /// The current suggestions, best first. Every query parameter is optional and falls back
    /// to the configured Recommendation defaults, so a plain GET is enough.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<List<CampaignSuggestionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CampaignSuggestionDto>>> GetAll(
        [FromQuery] RecommendationQueryDto query,
        CancellationToken cancellationToken)
    {
        return Ok(await recommendationService.GetSuggestionsAsync(query, cancellationToken));
    }
}
