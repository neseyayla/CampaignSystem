using CampaignSystem.DTOs;

namespace CampaignSystem.Services;

public interface ICampaignService
{
    Task<List<CampaignDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns null when no active campaign carries that id.</summary>
    Task<CampaignDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<CampaignDto> CreateAsync(CreateCampaignDto dto, CancellationToken cancellationToken = default);

    /// <summary>Returns false when no active campaign carries that id.</summary>
    Task<bool> UpdateAsync(int id, UpdateCampaignDto dto, CancellationToken cancellationToken = default);

    /// <summary>Soft delete — clears IsActive. Returns false when the campaign is not found.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

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
}
