namespace CampaignSystem.DTOs.Reports;

/// <summary>
/// One row of a detailed report. The columns shown depend on the report type: the reward reports
/// use <see cref="CampaignName"/> and a point <see cref="Amount"/>; the transaction report uses
/// <see cref="MerchantName"/> and a money <see cref="Amount"/>. Fields not relevant to a type are
/// left null.
/// </summary>
public record DetailReportRowDto(
    string CustomerNumber,
    int? CardId,
    string? CampaignName,
    DateTime Date,
    decimal Amount,
    string? MerchantName);
