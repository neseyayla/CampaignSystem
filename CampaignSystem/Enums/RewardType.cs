namespace CampaignSystem.Enums;

/// <summary>
/// What a CAMPAIGN_REWARD row records. Persisted as the member name.
///
/// Earn rows are what the batch grants and are unique per campaign, customer and card.
/// Clawback rows are written afterwards when a counted purchase is refunded; they carry a
/// negative point figure and there may be several, so the balance is the sum of all rows.
/// </summary>
public enum RewardType
{
    /// <summary>Points granted by the reward calculation. Positive.</summary>
    Earn = 1,

    /// <summary>Points taken back after a counted purchase was refunded. Negative.</summary>
    Clawback = 2
}
