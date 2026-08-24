using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Repositories;

namespace CampaignSystem.Services;

/// <summary>
/// TransactionCode business rules. No DbContext needed — every check is a single-table
/// question, which the repository already answers.
/// </summary>
public class TransactionCodeService(
    IRepository<TransactionCode> transactionCodes,
    IRepository<Transaction> transactions,
    IRepository<CampaignTransactionCode> campaignTransactionCodes) : ITransactionCodeService
{
    public async Task<List<TransactionCodeDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await transactionCodes.GetAllAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<TransactionCodeDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var transactionCode = await transactionCodes.GetByIdAsync(id);
        return transactionCode is null ? null : ToDto(transactionCode);
    }

    public async Task<ServiceResult<TransactionCodeDto>> CreateAsync(
        CreateTransactionCodeDto dto,
        CancellationToken cancellationToken = default)
    {
        // The database enforces this too, but catching it here produces a readable message
        // instead of a unique index violation.
        if (await transactionCodes.ExistsAsync(tc => tc.Code == dto.Code, cancellationToken))
        {
            return ServiceResult<TransactionCodeDto>.Conflict(
                $"'{dto.Code}' kodlu bir işlem tipi zaten mevcut. Lütfen farklı bir kod kullanın.");
        }

        var transactionCode = new TransactionCode
        {
            Code = dto.Code,
            Name = dto.Name
        };

        await transactionCodes.AddAsync(transactionCode, cancellationToken);
        await transactionCodes.SaveChangesAsync(cancellationToken);

        return ServiceResult<TransactionCodeDto>.Success(ToDto(transactionCode));
    }

    public async Task<ServiceResult> UpdateAsync(
        int id,
        UpdateTransactionCodeDto dto,
        CancellationToken cancellationToken = default)
    {
        var transactionCode = await transactionCodes.GetByIdAsync(id);

        if (transactionCode is null)
        {
            return ServiceResult.NotFound();
        }

        if (await transactionCodes.ExistsAsync(
                tc => tc.Id != id && tc.Code == dto.Code, cancellationToken))
        {
            return ServiceResult.Conflict(
                $"'{dto.Code}' kodu başka bir işlem tipi tarafından kullanılıyor. Lütfen farklı bir kod kullanın.");
        }

        transactionCode.Code = dto.Code;
        transactionCode.Name = dto.Name;

        transactionCodes.Update(transactionCode);
        await transactionCodes.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var transactionCode = await transactionCodes.GetByIdAsync(id);

        if (transactionCode is null)
        {
            return ServiceResult.NotFound();
        }

        // The database would reject this too (DeleteBehavior.Restrict on both relations),
        // but checking here produces a readable message instead of a raw FK violation.
        if (await transactions.ExistsAsync(t => t.TransactionCodeId == id, cancellationToken) ||
            await campaignTransactionCodes.ExistsAsync(
                ctc => ctc.TransactionCodeId == id, cancellationToken))
        {
            return ServiceResult.Conflict(
                "Bu işlem tipi kullanımda olduğu için silinemiyor (bağlı işlem veya kampanya var).");
        }

        transactionCodes.Remove(transactionCode);
        await transactionCodes.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    private static TransactionCodeDto ToDto(TransactionCode transactionCode) => new()
    {
        Id = transactionCode.Id,
        Code = transactionCode.Code,
        Name = transactionCode.Name
    };
}
