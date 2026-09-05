using CampaignSystem.DTOs;

namespace CampaignSystem.Services;

/// <summary>
/// Owns a campaign's terms — the human-readable sentences shown to an operator and, through
/// the catalog, to the customer. Split out of <see cref="ICampaignService"/> so campaign CRUD
/// and the condition/template concern each have one reason to change.
/// </summary>
public interface ICampaignConditionService
{
    /// <summary>
    /// The campaign's terms, in display order. Returns null when no active campaign
    /// carries that id.
    /// </summary>
    Task<List<CampaignConditionDto>?> GetConditionsAsync(
        int campaignId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the campaign's whole set of terms with the one given — an operator's edit,
    /// reorder, removal or free-hand addition. Returns false when no active campaign
    /// carries that id.
    /// </summary>
    Task<bool> SetConditionsAsync(
        int campaignId,
        List<CampaignConditionDto> conditions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuilds the campaign's terms from its current rules and criteria. Only replaces
    /// the previously auto-generated lines — anything an operator typed in by hand stays.
    /// Returns null when no active campaign carries that id.
    /// </summary>
    Task<List<CampaignConditionDto>?> GenerateConditionsAsync(
        int campaignId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The same sentences <see cref="GenerateConditionsAsync"/> would write, computed from a
    /// draft that has not been saved yet — what the campaign definition screen shows while
    /// the operator is still filling the form in.
    /// </summary>
    Task<List<string>> PreviewConditionsAsync(
        CampaignConditionsPreviewDto dto,
        CancellationToken cancellationToken = default);
}
