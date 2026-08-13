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
}
