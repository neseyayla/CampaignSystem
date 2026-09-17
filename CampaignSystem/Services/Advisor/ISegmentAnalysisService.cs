using CampaignSystem.DTOs.Advisor;

namespace CampaignSystem.Services.Advisor;

/// <summary>
/// Reads a customer segment's habits out of the transaction history: which merchant
/// categories its spend goes to, and how that compares with the bank as a whole.
///
/// It is the advisor's view of a segment — "farmers spend twice the bank's share on fuel" —
/// and, as a plain service behind an endpoint, a figure an operator can check without asking
/// the model anything.
/// </summary>
public interface ISegmentAnalysisService
{
    /// <param name="lookbackDays">Window to read, in days; 90 by default, clamped to 14–365.</param>
    Task<ServiceResult<SegmentBreakdownDto>> GetCategoryBreakdownAsync(
        int segmentId,
        int? lookbackDays = null,
        CancellationToken cancellationToken = default);
}
