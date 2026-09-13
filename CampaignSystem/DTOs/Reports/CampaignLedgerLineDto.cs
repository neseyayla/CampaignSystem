namespace CampaignSystem.DTOs.Reports;

/// <summary>
/// One aggregated line of a campaign's ledger summary: how many movements of a kind, their total,
/// and the date span they fall in. <see cref="Type"/> is a stable string the UI maps to a label
/// and a unit ("earn" and the two clawback kinds are points; "refund" is money). <see cref="Total"/>
/// keeps the reward sign (Earn positive, clawback negative); a refund is the positive amount.
/// </summary>
public record CampaignLedgerLineDto(
    string Type,
    int Count,
    decimal Total,
    DateTime? FirstDate,
    DateTime? LastDate);
