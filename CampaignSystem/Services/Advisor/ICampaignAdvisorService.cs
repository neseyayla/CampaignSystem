using CampaignSystem.DTOs.Advisor;

namespace CampaignSystem.Services.Advisor;

/// <summary>
/// Answers an operator's plain-language question about campaign opportunities by calling the
/// analysis services that already exist and explaining what they return.
///
/// The division of labour is deliberate and is the whole design: every figure comes from a
/// tool — a reviewed, tested query — and the model only decides which tool to call and how to
/// word the result. It is never handed transaction rows and never computes a total, because a
/// wrong aggregate here does not fail, it comes back as a confident number.
/// </summary>
public interface ICampaignAdvisorService
{
    Task<ServiceResult<AdvisorAnswerDto>> AskAsync(
        string question,
        CancellationToken cancellationToken = default);
}
