namespace CampaignSystem.DTOs;

/// <summary>
/// What one run of the daily batch did. Every figure is a count of campaigns, so an
/// operator can tell at a glance whether the run was quiet or busy.
/// </summary>
public class DailyBatchResultDto
{
    public DateTime RunAt { get; set; }

    /// <summary>Campaigns whose start date had arrived, moved from Pending to Ongoing.</summary>
    public int Started { get; set; }

    /// <summary>Campaigns whose end date had passed, moved from Ongoing to Loading.</summary>
    public int Closed { get; set; }

    /// <summary>Campaigns whose loading day had arrived and whose rewards were written.</summary>
    public int Loaded { get; set; }

    /// <summary>Reward rows written across all the campaigns loaded in this run.</summary>
    public int RewardsCreated { get; set; }

    public decimal TotalRewardPoint { get; set; }

    /// <summary>
    /// Reward rows reduced because a purchase they counted was refunded, across every campaign
    /// still inside its point-clawback window.
    /// </summary>
    public int RewardsReduced { get; set; }

    /// <summary>
    /// Campaigns that were due but could not be loaded, with the reason. The run carries on
    /// past a failure so that one bad campaign cannot hold up the rest.
    /// </summary>
    public List<DailyBatchFailureDto> Failures { get; set; } = [];
}

public class DailyBatchFailureDto
{
    public int CampaignId { get; set; }

    public string Reason { get; set; } = null!;
}
