namespace CampaignSystem.DTOs.Reports;

/// <summary>
/// The report payload: one row per campaign for the table, plus the totals for the summary cards,
/// both computed from the same filtered set so they always agree.
/// </summary>
public record CampaignReportResultDto(
    IReadOnlyList<CampaignReportSummaryDto> Campaigns,
    ReportTotalsDto Totals);
