namespace CampaignSystem.Configuration;

/// <summary>
/// Settings for the reward batch, bound from the "RewardCalculation" section.
/// </summary>
public class RewardCalculationOptions
{
    public const string SectionName = "RewardCalculation";

    /// <summary>
    /// How many days after a campaign ends its rewards are loaded. The business decides the
    /// value; nothing in the code assumes a particular one.
    ///
    /// A campaign stays in the Loading state for this long: its period is over, its
    /// qualifying transactions are already fixed, but the points have not been granted yet.
    /// Zero means the batch may run as soon as the campaign ends.
    /// </summary>
    public int DaysAfterCampaignEnd { get; set; }
}
