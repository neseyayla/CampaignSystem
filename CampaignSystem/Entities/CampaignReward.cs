using CampaignSystem.Enums;

namespace CampaignSystem.Entities;

/// <summary>
/// Result row written by the end-of-campaign batch job. Maps to CAMPAIGN_REWARD.
/// A unique constraint on (CampaignId, CustomerId, CardId) — applied only to Earn rows —
/// prevents double rewards at the database level if the batch job runs twice. A refund later
/// adds a negative <see cref="Enums.RewardType.Clawback"/> row rather than editing the Earn one.
/// </summary>
public class CampaignReward
{
    public long Id { get; set; }

    public int CampaignId { get; set; }

    public int CustomerId { get; set; }

    /// <summary>Null for a customer level reward.</summary>
    public int? CardId { get; set; }

    /// <summary>Whether this row is a granted reward or a later refund clawback.</summary>
    public RewardType RewardType { get; set; } = RewardType.Earn;

    /// <summary>
    /// Transactions this row accounts for. Positive on an Earn row (those that met every
    /// criterion); negative on a Clawback row (those dropped by a refund).
    /// </summary>
    public int QualifyingCount { get; set; }

    /// <summary>Points: positive when granted (after the cap), negative on a clawback.</summary>
    public decimal RewardPoint { get; set; }

    public DateTime RewardDate { get; set; }

    public Campaign Campaign { get; set; } = null!;

    public Customer Customer { get; set; } = null!;

    public Card? Card { get; set; }
}
