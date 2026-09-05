using CampaignSystem.Entities;

namespace CampaignSystem.Services;

/// <summary>One reward's worth of transactions. CardId is null at customer level.</summary>
public record RewardGroup(int CustomerId, int? CardId, int Count);

/// <summary>
/// The reward calculation core: the one rule that decides which transactions a campaign pays
/// on, and how they roll up into rewards. Shared by every reader — the live preview, the
/// end-of-campaign settlement and the refund reconciliation — so the figure shown to a
/// customer during a campaign and the points actually granted at the end cannot drift apart.
/// </summary>
public interface IRewardCalculator
{
    /// <summary>
    /// The transactions that meet every one of the campaign's conditions.
    ///
    /// A criteria table with no rows for the campaign is not a filter that matches nothing —
    /// it means the campaign places no restriction on that dimension. Each criterion is
    /// therefore only applied when the campaign actually names something.
    /// </summary>
    Task<List<Transaction>> QualifyingTransactions(
        Campaign campaign,
        CancellationToken cancellationToken,
        bool includeReversed = false,
        int? customerId = null);

    /// <summary>
    /// Groups the qualifying transactions at the level the campaign accumulates: one group
    /// per card, or one per customer with every card pooled.
    /// </summary>
    List<RewardGroup> Group(IEnumerable<Transaction> transactions, Campaign campaign);

    /// <summary>Caps an earned amount at the campaign's reward ceiling, when it has one.</summary>
    decimal ApplyCap(decimal earned, decimal? cap);
}
