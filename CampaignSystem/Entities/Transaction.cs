namespace CampaignSystem.Entities;

/// <summary>
/// Card transaction. Maps to TRANSACTION.
/// This is the table the evaluation batch job reads and the largest table in the system.
/// </summary>
public class Transaction
{
    public long Id { get; set; }

    /// <summary>Retrieval reference number — unique business key of the transaction.</summary>
    public string? Rrn { get; set; }

    public int CardId { get; set; }

    /// <summary>
    /// Stored even though it can be derived through <see cref="CardId"/>;
    /// this avoids a join in customer level aggregation.
    /// </summary>
    public int CustomerId { get; set; }

    public int? MerchantId { get; set; }

    public int TransactionCodeId { get; set; }

    public DateTime TransactionDate { get; set; }

    public decimal Amount { get; set; }

    public Card Card { get; set; } = null!;

    public Customer Customer { get; set; } = null!;

    public Merchant? Merchant { get; set; }

    public TransactionCode TransactionCode { get; set; } = null!;
}
