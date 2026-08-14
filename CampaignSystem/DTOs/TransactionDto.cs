namespace CampaignSystem.DTOs;

public class TransactionDto
{
    public long Id { get; set; }

    /// <summary>Retrieval reference number — the transaction's business key.</summary>
    public string? Rrn { get; set; }

    public int CardId { get; set; }

    public int CustomerId { get; set; }

    public int? MerchantId { get; set; }

    public int TransactionCodeId { get; set; }

    public DateTime TransactionDate { get; set; }

    public decimal Amount { get; set; }
}
