using CampaignSystem.DTOs;

namespace CampaignSystem.Services;

/// <summary>
/// Owns a campaign's scope — the five criteria junction tables (segment, product, merchant,
/// transaction code, clawback-exempt product). Split out of <see cref="ICampaignService"/> so
/// campaign CRUD and the multi-table criteria concern each have one reason to change.
/// </summary>
public interface ICampaignCriteriaService
{
    /// <summary>
    /// The campaign's current scope. Returns null when no active campaign carries that id.
    /// An empty list means the campaign is unrestricted on that dimension.
    /// </summary>
    Task<CampaignCriteriaDto?> GetCriteriaAsync(int campaignId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the campaign's whole scope with the one given. Criteria not present in the
    /// request are removed, so sending the same request twice leaves the same result.
    /// </summary>
    Task<SetCriteriaOutcome> SetCriteriaAsync(
        int campaignId,
        CampaignCriteriaDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears every child row of a campaign that is about to be removed outright — its five
    /// criteria tables and its condition rows, whose Restrict foreign keys would otherwise
    /// block the delete. Stages the removals on the shared context; the caller saves them in
    /// the same transaction as the campaign row itself.
    /// </summary>
    Task RemoveAllForCampaignAsync(int campaignId, CancellationToken cancellationToken = default);
}
