namespace CampaignSystem.Entities;

/// <summary>
/// Restricts a campaign to one or more transaction types. Maps to CAMPAIGN_TRANSACTION_CODE.
/// No rows for a campaign means every transaction type qualifies.
/// </summary>
public class CampaignTransactionCode
{
    public int CampaignId { get; set; }

    public int TransactionCodeId { get; set; }

    public Campaign Campaign { get; set; } = null!;

    public TransactionCode TransactionCode { get; set; } = null!;
}
