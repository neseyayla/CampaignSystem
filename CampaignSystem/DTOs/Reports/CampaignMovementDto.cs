namespace CampaignSystem.DTOs.Reports;

/// <summary>
/// One line of a campaign's movement ledger. <see cref="Type"/> is a stable string the UI maps to
/// a label and a unit: "earn" and the two clawback kinds are points; "refund" is money (TL).
/// <see cref="Amount"/> keeps the reward sign (Earn positive, clawback negative); a refund is the
/// positive amount returned.
/// </summary>
public record CampaignMovementDto(
    DateTime Date,
    string Type,
    string CustomerNumber,
    decimal Amount);
