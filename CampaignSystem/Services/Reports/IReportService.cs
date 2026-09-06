using CampaignSystem.DTOs.Reports;

namespace CampaignSystem.Services.Reports;

public interface IReportService
{
    /// <summary>
    /// Aggregated statistics for the campaign with the given id. Returns <c>null</c> when no
    /// campaign has that id.
    /// </summary>
    Task<CampaignReportDto?> GetCampaignReportByIdAsync(int campaignId, CancellationToken cancellationToken = default);
}
