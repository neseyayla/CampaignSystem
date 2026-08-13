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
}
