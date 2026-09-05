using CampaignSystem.DTOs;

namespace CampaignSystem.Services;

public interface ITransactionService
{
    /// <summary>
    /// Transactions, narrowed by whichever filters are supplied. All of them are optional;
    /// with none the whole table comes back, which is only sensible in development.
    /// </summary>
    Task<List<TransactionDto>> GetAllAsync(
        int? cardId = null,
        int? customerId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);

    Task<TransactionDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<ServiceResult<TransactionDto>> CreateAsync(
        CreateTransactionDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One customer's own transactions, newest first, with merchant and type names resolved
    /// for display. Backs the customer's profile spending history.
    /// </summary>
    Task<List<CustomerTransactionDto>> GetCustomerHistoryAsync(
        int customerId,
        CancellationToken cancellationToken = default);
}
