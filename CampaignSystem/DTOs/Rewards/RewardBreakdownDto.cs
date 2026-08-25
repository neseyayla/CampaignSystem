namespace CampaignSystem.DTOs;

/// <summary>
/// The purchases behind one campaign's reward for a customer, shown as a drill-down under
/// "Kazandıklarım". Each earning purchase carries the points it produced; a refunded one is
/// flagged so the screen can show it in red.
///
/// Re-derived from the same criteria the reward engine uses — the link between a reward and
/// its transactions is not stored — so what is shown here always matches what was paid.
/// </summary>
public class RewardBreakdownDto
{
    public int CampaignId { get; set; }

    public string CampaignName { get; set; } = null!;

    /// <summary>Points one qualifying purchase earns, before any campaign cap.</summary>
    public decimal RewardPointPerTransaction { get; set; }

    public List<RewardBreakdownLineDto> Lines { get; set; } = [];
}

public class RewardBreakdownLineDto
{
    public long TransactionId { get; set; }

    public DateTime TransactionDate { get; set; }

    public decimal Amount { get; set; }

    public string? MerchantName { get; set; }

    /// <summary>
    /// The campaign's per-transaction points. Always positive; the screen shows it green when
    /// earned and red with a minus when <see cref="IsReversed"/>.
    /// </summary>
    public decimal RewardPoint { get; set; }

    /// <summary>True when this purchase was refunded, so its points were (or will be) taken back.</summary>
    public bool IsReversed { get; set; }
}
