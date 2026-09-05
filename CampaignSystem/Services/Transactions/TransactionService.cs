using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Repositories;

namespace CampaignSystem.Services;

/// <summary>
/// Records the transactions the reward calculation later reads.
///
/// There is no update and no delete: a transaction is a statement of something that
/// happened. Correcting it would silently change a reward that has already been paid, so
/// the card system sends a reversing transaction instead of editing the original.
/// </summary>
public class TransactionService(
    IRepository<Transaction> transactions,
    IRepository<Card> cards,
    IRepository<Merchant> merchants,
    IRepository<TransactionCode> transactionCodes) : ITransactionService
{
    public async Task<List<TransactionDto>> GetAllAsync(
        int? cardId = null,
        int? customerId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await transactions.FindAsync(
            t => (cardId == null || t.CardId == cardId)
                 && (customerId == null || t.CustomerId == customerId)
                 && (from == null || t.TransactionDate >= from)
                 && (to == null || t.TransactionDate <= to),
            cancellationToken);

        return rows.OrderByDescending(t => t.TransactionDate).Select(ToDto).ToList();
    }

    public async Task<TransactionDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var transaction = await transactions.GetByIdAsync(id);
        return transaction is null ? null : ToDto(transaction);
    }

    public async Task<ServiceResult<TransactionDto>> CreateAsync(
        CreateTransactionDto dto,
        CancellationToken cancellationToken = default)
    {
        var card = await cards.GetByIdAsync(dto.CardId);

        if (card is null || !card.IsActive)
        {
            return ServiceResult<TransactionDto>.Invalid($"Unknown or inactive card id: {dto.CardId}.");
        }

        if (!await transactionCodes.ExistsAsync(c => c.Id == dto.TransactionCodeId, cancellationToken))
        {
            return ServiceResult<TransactionDto>.Invalid(
                $"Unknown transaction code id: {dto.TransactionCodeId}.");
        }

        if (dto.MerchantId is not null &&
            !await merchants.ExistsAsync(m => m.Id == dto.MerchantId, cancellationToken))
        {
            return ServiceResult<TransactionDto>.Invalid($"Unknown merchant id: {dto.MerchantId}.");
        }

        // The unique index on Rrn is the real guard; this check turns a constraint
        // violation into a message that says what actually went wrong.
        if (dto.Rrn is not null &&
            await transactions.ExistsAsync(t => t.Rrn == dto.Rrn, cancellationToken))
        {
            return ServiceResult<TransactionDto>.Conflict(
                $"A transaction with reference number {dto.Rrn} has already been recorded.");
        }

        var transaction = new Transaction
        {
            Rrn = dto.Rrn,
            CardId = dto.CardId,

            // Read off the card rather than taken from the caller, so the two can never
            // disagree.
            CustomerId = card.CustomerId,

            MerchantId = dto.MerchantId,
            TransactionCodeId = dto.TransactionCodeId,
            TransactionDate = dto.TransactionDate,
            Amount = dto.Amount
        };

        await transactions.AddAsync(transaction, cancellationToken);
        await transactions.SaveChangesAsync(cancellationToken);

        return ServiceResult<TransactionDto>.Success(ToDto(transaction));
    }

    public async Task<List<CustomerTransactionDto>> GetCustomerHistoryAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        var rows = await transactions.FindAsync(t => t.CustomerId == customerId, cancellationToken);

        // Reference tables are small, so read them whole once and map in memory rather than
        // joining per row.
        var merchantNames = (await merchants.GetAllAsync(cancellationToken))
            .ToDictionary(m => m.Id, m => m.MerchantName);

        var typeNames = (await transactionCodes.GetAllAsync(cancellationToken))
            .ToDictionary(c => c.Id, c => c.Name);

        return rows
            .OrderByDescending(t => t.TransactionDate)
            .Select(t => new CustomerTransactionDto
            {
                Id = t.Id,
                TransactionDate = t.TransactionDate,
                Amount = t.Amount,
                CardId = t.CardId,
                MerchantName = t.MerchantId is not null
                    ? merchantNames.GetValueOrDefault(t.MerchantId.Value)
                    : null,
                TypeName = typeNames.GetValueOrDefault(t.TransactionCodeId, "—"),
                IsRefund = t.OriginalTransactionId is not null
            })
            .ToList();
    }

    private static TransactionDto ToDto(Transaction transaction) => new()
    {
        Id = transaction.Id,
        Rrn = transaction.Rrn,
        CardId = transaction.CardId,
        CustomerId = transaction.CustomerId,
        MerchantId = transaction.MerchantId,
        TransactionCodeId = transaction.TransactionCodeId,
        TransactionDate = transaction.TransactionDate,
        Amount = transaction.Amount
    };
}
