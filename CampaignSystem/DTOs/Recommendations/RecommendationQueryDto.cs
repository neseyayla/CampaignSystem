namespace CampaignSystem.DTOs;

/// <summary>
/// Optional overrides for a single recommendation request. Anything left null falls back to
/// the configured <c>Recommendation</c> defaults, so a plain GET needs no query string.
/// </summary>
public class RecommendationQueryDto
{
    /// <summary>Days of history to read spend and trend from. Clamped to a sane range server-side.</summary>
    public int? LookbackDays { get; set; }

    /// <summary>Days ahead the suggested campaign is assumed to run.</summary>
    public int? HorizonDays { get; set; }

    /// <summary>Drop categories below this much spend in the lookback window.</summary>
    public decimal? MinimumSpend { get; set; }

    /// <summary>Cap on how many suggestions come back.</summary>
    public int? MaxSuggestions { get; set; }

    /// <summary>Also return categories an open or upcoming campaign already covers. Default false.</summary>
    public bool IncludeCovered { get; set; }
}
