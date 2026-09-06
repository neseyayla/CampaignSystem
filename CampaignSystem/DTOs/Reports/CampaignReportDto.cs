namespace CampaignSystem.DTOs.Reports;

/// <summary>
/// Aggregated statistics for a single campaign, shown on the report screen: how many customers
/// earned, how many points were granted, how many were later clawed back, and how much money
/// was refunded on the purchases behind those rewards.
/// </summary>
public record CampaignReportDto(
    int CampaignId,
    string CampaignName,
    int CustomerCount,
    decimal TotalPointsEarned,
    decimal TotalPointsClawedBack,
    decimal TotalRefundAmount);
