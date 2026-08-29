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

    /// <summary>
    /// The campaign's minimum spend, or null when it has none. Sent so the screen can explain
    /// why a partially-refunded purchase dropped out: its amount net of refunds fell below this.
    /// </summary>
    public decimal? MinimumAmount { get; set; }

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

    /// <summary>
    /// The purchase amount net of every refund against it. Equal to <see cref="Amount"/> when
    /// nothing was refunded; lower after a partial refund; near zero after a full one.
    /// </summary>
    public decimal EffectiveAmount { get; set; }

    /// <summary>
    /// True only when refunds actually cost this purchase its points — i.e. the effective
    /// amount no longer qualifies. A partial refund that leaves it above the minimum keeps its
    /// points, so this stays false and the screen shows it green.
    /// </summary>
    public bool IsReversed { get; set; }

    /// <summary>The refunds against this purchase, oldest first. Empty when it was not refunded.</summary>
    public List<RefundLineDto> Refunds { get; set; } = [];
}

/// <summary>One İade transaction against a purchase. Amount is negative, as it is stored.</summary>
public class RefundLineDto
{
    public DateTime Date { get; set; }

    public decimal Amount { get; set; }
}
