namespace CampaignSystem.DTOs;

/// <summary>
/// One line of a customer's own spending history, for the profile screen. Merchant and type
/// names are resolved so the customer reads "BİM · Satış" rather than ids, and a refund is
/// flagged so the screen can set it apart from a purchase.
/// </summary>
public class CustomerTransactionDto
{
    public long Id { get; set; }

    public DateTime TransactionDate { get; set; }

    public decimal Amount { get; set; }

    public int CardId { get; set; }

    public string? MerchantName { get; set; }

    /// <summary>The transaction type in words: Satış, Nakit Avans, İade…</summary>
    public string TypeName { get; set; } = null!;

    /// <summary>True when this row reverses an earlier purchase.</summary>
    public bool IsRefund { get; set; }
}
