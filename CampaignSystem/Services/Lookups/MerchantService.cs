using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Repositories;

namespace CampaignSystem.Services;

/// <summary>
/// Merchant business rules. No DbContext needed — every check is a single-table question,
/// which the repository already answers.
/// </summary>
public class MerchantService(IRepository<Merchant> merchants) : IMerchantService
{
    public async Task<List<MerchantDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await merchants.FindAsync(m => m.IsActive, cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<MerchantDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var merchant = await merchants.GetByIdAsync(id);
        return merchant is null || !merchant.IsActive ? null : ToDto(merchant);
    }

    public async Task<ServiceResult<MerchantDto>> CreateAsync(
        CreateMerchantDto dto,
        CancellationToken cancellationToken = default)
    {
        // The database enforces this too, but catching it here produces a readable message
        // instead of a unique index violation.
        if (await merchants.ExistsAsync(m => m.MerchantNumber == dto.MerchantNumber, cancellationToken))
        {
            return ServiceResult<MerchantDto>.Conflict(
                $"'{dto.MerchantNumber}' numaralı bir üye işyeri zaten mevcut. Lütfen farklı bir numara kullanın.");
        }

        var merchant = new Merchant
        {
            MerchantNumber = dto.MerchantNumber,
            MerchantName = dto.MerchantName,
            IsActive = true
        };

        await merchants.AddAsync(merchant, cancellationToken);
        await merchants.SaveChangesAsync(cancellationToken);

        return ServiceResult<MerchantDto>.Success(ToDto(merchant));
    }

    public async Task<ServiceResult> UpdateAsync(
        int id,
        UpdateMerchantDto dto,
        CancellationToken cancellationToken = default)
    {
        var merchant = await merchants.GetByIdAsync(id);

        if (merchant is null || !merchant.IsActive)
        {
            return ServiceResult.NotFound();
        }

        merchant.MerchantName = dto.MerchantName;

        merchants.Update(merchant);
        await merchants.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var merchant = await merchants.GetByIdAsync(id);

        if (merchant is null || !merchant.IsActive)
        {
            return ServiceResult.NotFound();
        }

        // Soft delete. The merchant's transactions and campaign scoping stay intact.
        merchant.IsActive = false;

        merchants.Update(merchant);
        await merchants.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    private static MerchantDto ToDto(Merchant merchant) => new()
    {
        Id = merchant.Id,
        MerchantNumber = merchant.MerchantNumber,
        MerchantName = merchant.MerchantName,
        IsActive = merchant.IsActive
    };
}
