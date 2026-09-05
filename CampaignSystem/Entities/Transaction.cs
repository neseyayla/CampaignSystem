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

    /// <summary>
    /// Set only on a refund row: the original purchase this İade transaction reverses. Null on
    /// an ordinary transaction. A purchase counts as refunded when such a row points at it —
    /// there is no stored flag, the fact is derived from the refund's existence.
    /// </summary>
    public long? OriginalTransactionId { get; set; }

    /// <summary>
    /// Set only on a refund row, once the reward batch has accounted for it. Null means the
    /// batch has not yet processed this refund. It gates which campaigns the nightly clawback
    /// re-checks — it never changes the maths: the effective amount always sums every refund,
    /// processed or not.
    /// </summary>
    public DateTime? ClawbackProcessedAt { get; set; }

    public Card Card { get; set; } = null!;

    public Customer Customer { get; set; } = null!;

    public Merchant? Merchant { get; set; }

    public TransactionCode TransactionCode { get; set; } = null!;

    /// <summary>The original purchase, when this row is a refund. Null otherwise.</summary>
    public Transaction? OriginalTransaction { get; set; }
}
