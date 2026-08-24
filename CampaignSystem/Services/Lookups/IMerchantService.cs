using CampaignSystem.DTOs;

namespace CampaignSystem.Services;

public interface IMerchantService
{
    Task<List<MerchantDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<MerchantDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ServiceResult<MerchantDto>> CreateAsync(
        CreateMerchantDto dto,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(
        int id,
        UpdateMerchantDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>Soft delete — clears IsActive.</summary>
    Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
