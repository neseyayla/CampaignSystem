using CampaignSystem.DTOs;

namespace CampaignSystem.Services;

public interface IProductService
{
    Task<List<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ProductDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductDto>> CreateAsync(
        CreateProductDto dto,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(
        int id,
        UpdateProductDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>Hard delete — Product carries no IsActive flag.</summary>
    Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
