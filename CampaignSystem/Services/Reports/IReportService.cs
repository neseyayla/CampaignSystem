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
    /// One campaign's movement ledger, ordered by date: loaded rewards, refund transactions and
    /// the point clawbacks. Narrowed to the requested kind.
    /// </summary>
    Task<IReadOnlyList<CampaignMovementDto>> GetCampaignMovementsAsync(
        int campaignId,
        MovementFilter type,
        CancellationToken cancellationToken = default);
}
