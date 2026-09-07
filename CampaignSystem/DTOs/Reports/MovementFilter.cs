namespace CampaignSystem.DTOs.Reports;

/// <summary>
/// Which kinds of movement the campaign ledger returns. Maps to the tabs on the movements panel.
/// </summary>
public enum MovementFilter
{
    /// <summary>Every movement.</summary>
    All = 0,

    /// <summary>Loaded rewards (RewardType.Earn).</summary>
    Earn = 1,

    /// <summary>Refund transactions (money).</summary>
    Refund = 2,

    /// <summary>Point clawbacks — both refund-driven and unused-points.</summary>
    Clawback = 3
}
