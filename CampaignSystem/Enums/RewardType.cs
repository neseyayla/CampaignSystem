namespace CampaignSystem.Enums;

/// <summary>
/// What a CAMPAIGN_REWARD row records. Persisted as the member name.
///
/// Earn rows are what the batch grants and are unique per campaign, customer and card.
/// Clawback and UnusedPointsClawback rows are written afterwards; they carry a negative point
/// figure and there may be several, so the balance is the sum of all rows.
/// </summary>
public enum RewardType
{
    /// <summary>Points granted by the reward calculation. Positive.</summary>
    Earn = 1,

    /// <summary>Points taken back after a counted purchase was refunded. Negative.</summary>
    Clawback = 2,

    /// <summary>
    /// Points taken back because they were never redeemed within the campaign's unused-points
    /// window. Negative. Kept distinct from <see cref="Clawback"/> so the reason a reward
    /// balance dropped stays visible.
    /// </summary>
    UnusedPointsClawback = 3
}
