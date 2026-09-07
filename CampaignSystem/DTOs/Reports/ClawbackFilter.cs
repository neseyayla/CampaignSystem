namespace CampaignSystem.DTOs.Reports;

/// <summary>
/// How the report is narrowed by a campaign's clawback settings, matching the "Geri Alım" filter.
/// The flags come from the campaign definition, not from whether any clawback actually happened.
/// </summary>
public enum ClawbackFilter
{
    /// <summary>No clawback filter.</summary>
    All = 0,

    /// <summary>Campaigns with refund clawback enabled.</summary>
    Refund = 1,

    /// <summary>Campaigns with unused-points clawback enabled.</summary>
    Unused = 2
}
