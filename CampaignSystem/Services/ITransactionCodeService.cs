using CampaignSystem.DTOs;

namespace CampaignSystem.Services;

public interface ITransactionCodeService
{
    Task<List<TransactionCodeDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<TransactionCodeDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ServiceResult<TransactionCodeDto>> CreateAsync(
        CreateTransactionCodeDto dto,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(
        int id,
        UpdateTransactionCodeDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>Hard delete — TransactionCode carries no IsActive flag.</summary>
    Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
