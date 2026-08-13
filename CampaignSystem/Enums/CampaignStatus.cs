namespace CampaignSystem.Enums;

/// <summary>
/// Where a campaign stands in its lifecycle. The states follow the batch pipeline rather
/// than an approval chain: a campaign waits for its start date, runs, has its rewards
/// calculated, and is then done.
///
/// Persisted as the member name.
/// </summary>
public enum CampaignStatus
{
    /// <summary>
    /// Created, but <see cref="Entities.Campaign.StartDate"/> has not arrived yet.
    /// Transactions are not accumulating.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Running. Transactions falling inside the campaign period and matching its criteria
    /// count towards a reward.
    /// </summary>
    Ongoing = 2,

    /// <summary>
    /// <see cref="Entities.Campaign.EndDate"/> has passed and the reward batch is working
    /// through the campaign. Rewards are not final until it finishes.
    /// </summary>
    Loading = 3,

    /// <summary>
    /// Rewards have been calculated and loaded. The campaign is closed.
    /// </summary>
    Ended = 4
}
