namespace CampaignSystem.Entities;

/// <summary>
/// Transaction type such as sale, cash advance or debt payment. Maps to TRANSACTION_CODE.
/// </summary>
public class TransactionCode
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public ICollection<Transaction> Transactions { get; set; } = [];

    public ICollection<CampaignTransactionCode> CampaignTransactionCodes { get; set; } = [];
}
