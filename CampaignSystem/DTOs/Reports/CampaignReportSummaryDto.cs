namespace CampaignSystem.DTOs.Reports;

/// <summary>
/// One row of the campaign report table. Points are split by how they left the customer:
/// <see cref="RefundClawbackPoints"/> were taken back because a counted purchase was refunded
/// (RewardType.Clawback), <see cref="UnusedClawbackPoints"/> because they were never redeemed
/// (RewardType.UnusedPointsClawback). Net is what is left after both. The two Enabled flags
/// carry the campaign's own settings, so the report can be filtered by how each campaign was
/// configured, independently of whether any clawback actually happened.
/// </summary>
public record CampaignReportSummaryDto(
    int CampaignId,
    string CampaignName,
    int CustomerCount,
    decimal TotalPointsEarned,
    decimal RefundClawbackPoints,
    decimal UnusedClawbackPoints,
    decimal NetPoints,
    bool RefundClawbackEnabled,
    bool UnusedClawbackEnabled);
