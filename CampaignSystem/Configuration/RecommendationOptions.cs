namespace CampaignSystem.Configuration;

/// <summary>
/// Settings for the campaign recommendation engine, bound from the "Recommendation" section.
///
/// These are the only knobs the heuristic exposes, and tuning them is what "training" means
/// for it right now. A later model can read the same transaction history and replace the
/// scoring outright without any of these names leaking into its callers.
/// </summary>
public class RecommendationOptions
{
    public const string SectionName = "Recommendation";

    /// <summary>
    /// How far back the spend and trend figures look, in days. The window is split in half to
    /// read a trend: the more recent half against the one before it.
    /// </summary>
    public int LookbackDays { get; set; } = 90;

    /// <summary>
    /// How many days ahead a suggested campaign is assumed to run. Decides which months'
    /// seasonal weights are averaged into a suggestion's score, and the dates that prefill
    /// the campaign form.
    /// </summary>
    public int HorizonDays { get; set; } = 45;

    /// <summary>A category is left out below this much qualifying spend in the lookback window.</summary>
    public decimal MinimumSpend { get; set; } = 1000m;

    /// <summary>How many suggestions the endpoint returns at most, best first.</summary>
    public int MaxSuggestions { get; set; } = 10;

    /// <summary>Weight of the normalised spend volume in the score.</summary>
    public double SpendWeight { get; set; } = 1.0;

    /// <summary>Weight of the recent spend trend — the more recent half against the half before it.</summary>
    public double TrendWeight { get; set; } = 1.5;

    /// <summary>Weight of the seasonal uplift expected over the campaign horizon.</summary>
    public double SeasonWeight { get; set; } = 1.25;

    /// <summary>
    /// Extra multiplier for a category that no open or upcoming campaign already covers. The
    /// engine exists to surface these, so an uncovered category outranks a covered one at the
    /// same spend.
    /// </summary>
    public double CoverageGapBoost { get; set; } = 1.75;

    /// <summary>
    /// Fraction of a category's average qualifying transaction that becomes the suggested
    /// RewardPoint on the prefilled form. A starting point for the operator, nothing binding.
    /// </summary>
    public double SuggestedRewardRate { get; set; } = 0.02;
}
