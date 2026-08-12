namespace CampaignSystem.Enums;

/// <summary>
/// The level at which qualifying transactions accumulate, which in turn decides
/// how many reward rows a campaign produces for one customer.
/// Persisted as the string codes shown below.
/// </summary>
public enum EarningType
{
    /// <summary>
    /// K — accumulate per card. A customer with three cards can earn three separate
    /// rewards, and <see cref="Entities.CampaignReward.CardId"/> is populated.
    /// </summary>
    CardBased = 1,

    /// <summary>
    /// M — accumulate per customer. Transactions across all of the customer's cards
    /// are pooled into a single reward, and <see cref="Entities.CampaignReward.CardId"/>
    /// is left null.
    /// </summary>
    CustomerBased = 2
}
