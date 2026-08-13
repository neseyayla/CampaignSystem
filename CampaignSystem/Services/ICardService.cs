using CampaignSystem.DTOs;

namespace CampaignSystem.Services;

public interface ICardService
{
    /// <summary>Every active card, or only one customer's cards when customerId is given.</summary>
    Task<List<CardDto>> GetAllAsync(int? customerId = null, CancellationToken cancellationToken = default);

    Task<CardDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ServiceResult<CardDto>> CreateAsync(
        CreateCardDto dto,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(
        int id,
        UpdateCardDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>Soft delete — clears IsActive.</summary>
    Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
