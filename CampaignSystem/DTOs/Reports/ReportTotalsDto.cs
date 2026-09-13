namespace CampaignSystem.DTOs.Reports;

/// <summary>
/// The summary-card figures across all campaigns matching the current filters.
/// <see cref="DistinctCustomerCount"/> counts each customer once even when they earned in several
/// campaigns, unlike a sum of the per-campaign counts.
/// </summary>
public record ReportTotalsDto(
    int CampaignCount,
    int DistinctCustomerCount,
    decimal TotalPointsEarned,
    decimal TotalPointsClawedBack,
    decimal NetPoints);
