using CampaignSystem.DTOs;

namespace CampaignSystem.Services;

/// <summary>
/// The end-of-day job. Runs once a day and carries every campaign as far as its dates allow:
/// starts the ones that have begun, closes the ones that have finished, and loads the rewards
/// of the ones whose loading day has come.
///
/// Nothing else moves a campaign's status, which is what keeps the stored value honest.
/// </summary>
public interface IDailyBatchService
{
    Task<DailyBatchResultDto> RunAsync(CancellationToken cancellationToken = default);
}
