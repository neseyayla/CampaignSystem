using CampaignSystem.DTOs;

namespace CampaignSystem.Services;

/// <summary>
/// Turns recent card-transaction history into a ranked list of campaign ideas for an
/// operator: which merchant categories are worth defining a campaign over, given how much
/// is spent there, whether that spend is rising, the season ahead, and whether an open or
/// upcoming campaign already targets them.
///
/// The scoring is a transparent heuristic whose knobs live in
/// <see cref="CampaignSystem.Configuration.RecommendationOptions"/>. It is kept behind this
/// interface so a trained model can replace it later without the controller or the screen
/// changing.
/// </summary>
public interface ICampaignRecommendationService
{
    Task<List<CampaignSuggestionDto>> GetSuggestionsAsync(
        RecommendationQueryDto query,
        CancellationToken cancellationToken = default);
}
