using CampaignSystem.DTOs.Reports;

namespace CampaignSystem.Services.Reports;

public interface IReportService
{
    /// <summary>
    /// Report rows for the campaigns matching the given filters, ordered by name: customer count,
    /// points earned, refund-driven clawback points, unused-points clawback and the net left.
    /// A campaign is included only if it has at least one reward row.
    /// </summary>
    /// <param name="campaignId">A single campaign, or null for all.</param>
    /// <param name="clawback">Narrow by the campaign's clawback settings.</param>
    Task<CampaignReportResultDto> GetCampaignSummariesAsync(
        int? campaignId,
        ClawbackFilter clawback,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One campaign's ledger summary: an aggregated line each for loaded rewards, refund
    /// transactions, refund-driven clawbacks and unused-points clawbacks.
    /// </summary>
    Task<IReadOnlyList<CampaignLedgerLineDto>> GetCampaignLedgerAsync(
        int campaignId,
        CancellationToken cancellationToken = default);
}
