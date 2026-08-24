using CampaignSystem.DTOs;

namespace CampaignSystem.Services;

/// <summary>
/// The customer's own view of the campaign catalogue.
///
/// Separate from <see cref="ICampaignService"/> because it answers a different question.
/// The administrative service asks what campaigns exist; this one asks which of them a
/// particular person could earn from, and shows only those.
/// </summary>
public interface ICustomerCampaignService
{
    /// <summary>
    /// The running and upcoming campaigns this customer could earn from, oldest start
    /// first. Returns null when no active customer carries that id.
    /// </summary>
    Task<List<CustomerCampaignDto>?> GetEligibleAsync(
        int customerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One campaign with the customer's standing in it.
    ///
    /// NotFound covers three cases at once: the campaign does not exist, it is no longer
    /// open, or this customer was never eligible for it. From the customer's side those
    /// are the same answer, and telling them apart would disclose campaigns aimed at
    /// somebody else.
    /// </summary>
    Task<ServiceResult<CustomerCampaignDetailDto>> GetOneAsync(
        int customerId,
        int campaignId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs the customer up for a campaign that requires it.
    ///
    /// Eligibility is checked before the enrollment is written. The administrative
    /// endpoint deliberately does not do this — a branch may have a reason to enroll
    /// someone the criteria would not reach — but a request arriving from the customer's
    /// own screen has no such licence.
    /// </summary>
    Task<ServiceResult<ParticipationDto>> EnrollAsync(
        int customerId,
        int campaignId,
        int? cardId,
        CancellationToken cancellationToken = default);
}
