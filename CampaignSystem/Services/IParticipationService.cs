using CampaignSystem.DTOs;

namespace CampaignSystem.Services;

public interface IParticipationService
{
    /// <summary>
    /// The campaign's enrollments. Returns null when no active campaign carries that id.
    /// Cancelled enrollments are included, since they are part of the record.
    /// </summary>
    Task<List<ParticipationDto>?> GetByCampaignAsync(
        int campaignId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ParticipationDto>> EnrollAsync(
        int campaignId,
        CreateParticipationDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an enrollment Cancelled. Administrative — a customer cannot withdraw, so this
    /// exists to invalidate an enrollment that no longer meets the campaign's criteria, or
    /// to correct a mistake.
    /// </summary>
    Task<ServiceResult> CancelAsync(
        int campaignId,
        long participationId,
        CancellationToken cancellationToken = default);
}
