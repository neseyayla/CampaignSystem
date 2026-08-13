using CampaignSystem.DTOs;

namespace CampaignSystem.Services;

public interface ICustomerService
{
    Task<List<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CustomerDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ServiceResult<CustomerDto>> CreateAsync(
        CreateCustomerDto dto,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(
        int id,
        UpdateCustomerDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>Soft delete — clears IsActive.</summary>
    Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
