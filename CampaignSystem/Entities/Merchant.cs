namespace CampaignSystem.Entities;

/// <summary>
/// Merchant that accepts card transactions. Maps to MERCHANT.
/// </summary>
public class Merchant
{
    public int Id { get; set; }

    /// <summary>BKM merchant identifier.</summary>
    public string MerchantNumber { get; set; } = null!;

    public string MerchantName { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public ICollection<Transaction> Transactions { get; set; } = [];

    public ICollection<CampaignMerchant> CampaignMerchants { get; set; } = [];
}
