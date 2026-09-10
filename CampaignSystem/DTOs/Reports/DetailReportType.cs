namespace CampaignSystem.DTOs.Reports;

/// <summary>
/// Which detailed, row-level report the "Detaylı Rapor" screen asks for. Mirrors the legacy
/// report-type radios.
/// </summary>
public enum DetailReportType
{
    /// <summary>Loaded rewards (Yükleme) — RewardType.Earn rows.</summary>
    Loaded = 1,

    /// <summary>Unused-points clawback (Geri Alım) — RewardType.UnusedPointsClawback rows.</summary>
    UnusedClawback = 2,

    /// <summary>Refund-driven clawback (İade) — RewardType.Clawback rows.</summary>
    RefundClawback = 3,

    /// <summary>Card transactions (İşlem) — TRANSACTION rows.</summary>
    Transaction = 4
}
