using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Enums;
using CampaignSystem.Repositories;

namespace CampaignSystem.Services;

/// <summary>
/// Enrollment rules for campaigns that require one.
///
/// Enrollment records the customer's intent to take part. Whether the customer's
/// transactions actually qualify is decided later, when rewards are calculated — that
/// answer is not stored here.
/// </summary>
public class ParticipationService(
    IRepository<CampaignParticipation> participations,
    IRepository<Campaign> campaigns,
    IRepository<Customer> customers,
    IRepository<Card> cards) : IParticipationService
{
    public async Task<List<ParticipationDto>?> GetByCampaignAsync(
        int campaignId,
        CancellationToken cancellationToken = default)
    {
        if (!await campaigns.ExistsAsync(c => c.Id == campaignId && c.IsActive, cancellationToken))
        {
            return null;
        }

        var rows = await participations.FindAsync(p => p.CampaignId == campaignId, cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    public async Task<ServiceResult<ParticipationDto>> EnrollAsync(
        int campaignId,
        CreateParticipationDto dto,
        CancellationToken cancellationToken = default)
    {
        var campaign = await campaigns.GetByIdAsync(campaignId);

        if (campaign is null || !campaign.IsActive)
        {
            return ServiceResult<ParticipationDto>.NotFound();
        }

        // Everyone is already in a mass campaign, so there is nothing to enroll in.
        if (campaign.CampaignType != CampaignType.EnrollmentRequired)
        {
            return ServiceResult<ParticipationDto>.Invalid(
                "This campaign does not require enrollment — every eligible customer takes part automatically.");
        }

        // Once the campaign is being evaluated or is over, a new enrollment cannot change
        // anything and would only distort the record.
        if (campaign.Status is not (CampaignStatus.Pending or CampaignStatus.Ongoing))
        {
            return ServiceResult<ParticipationDto>.Invalid(
                $"The campaign is {campaign.Status} and no longer accepts enrollments.");
        }

        if (!await customers.ExistsAsync(c => c.Id == dto.CustomerId && c.IsActive, cancellationToken))
        {
            return ServiceResult<ParticipationDto>.Invalid(
                $"Unknown or inactive customer id: {dto.CustomerId}.");
        }

        var levelError = await ValidateLevelAsync(campaign, dto, cancellationToken);

        if (levelError is not null)
        {
            return ServiceResult<ParticipationDto>.Invalid(levelError);
        }

        // The database enforces this with a unique index; checking here turns it into a
        // clear message instead of a constraint violation.
        var alreadyEnrolled = await participations.ExistsAsync(
            p => p.CampaignId == campaignId
                 && p.CustomerId == dto.CustomerId
                 && p.CardId == dto.CardId,
            cancellationToken);

        if (alreadyEnrolled)
        {
            return ServiceResult<ParticipationDto>.Conflict(
                "This customer is already enrolled in the campaign at that level.");
        }

        var participation = new CampaignParticipation
        {
            CampaignId = campaignId,
            CustomerId = dto.CustomerId,
            CardId = dto.CardId,
            ParticipationDate = DateTime.Now,
            Status = ParticipationStatus.Active
        };

        await participations.AddAsync(participation, cancellationToken);
        await participations.SaveChangesAsync(cancellationToken);

        return ServiceResult<ParticipationDto>.Success(ToDto(participation));
    }

    public async Task<ServiceResult> CancelAsync(
        int campaignId,
        long participationId,
        CancellationToken cancellationToken = default)
    {
        var participation = await participations.GetByIdAsync(participationId);

        if (participation is null || participation.CampaignId != campaignId)
        {
            return ServiceResult.NotFound();
        }

        if (participation.Status == ParticipationStatus.Cancelled)
        {
            return ServiceResult.Invalid("This enrollment is already cancelled.");
        }

        participation.Status = ParticipationStatus.Cancelled;

        participations.Update(participation);
        await participations.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    /// <summary>
    /// Enforces that the enrollment level matches how the campaign accumulates. A card
    /// level campaign that does not know which card, or a customer level campaign pinned
    /// to one card, cannot be evaluated coherently.
    /// </summary>
    private async Task<string?> ValidateLevelAsync(
        Campaign campaign,
        CreateParticipationDto dto,
        CancellationToken cancellationToken)
    {
        if (campaign.EarningType == EarningType.CustomerBased)
        {
            return dto.CardId is null
                ? null
                : "This campaign accumulates per customer, so CardId must be omitted.";
        }

        if (dto.CardId is null)
        {
            return "This campaign accumulates per card, so CardId is required.";
        }

        var card = await cards.GetByIdAsync(dto.CardId.Value);

        if (card is null || !card.IsActive)
        {
            return $"Unknown or inactive card id: {dto.CardId}.";
        }

        if (card.CustomerId != dto.CustomerId)
        {
            return $"Card {dto.CardId} does not belong to customer {dto.CustomerId}.";
        }

        return null;
    }

    private static ParticipationDto ToDto(CampaignParticipation participation) => new()
    {
        Id = participation.Id,
        CampaignId = participation.CampaignId,
        CustomerId = participation.CustomerId,
        CardId = participation.CardId,
        ParticipationDate = participation.ParticipationDate,
        Status = participation.Status
    };
}
