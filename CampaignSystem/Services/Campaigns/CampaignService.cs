using CampaignSystem.Data;
using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Enums;
using CampaignSystem.Repositories;
using CampaignSystem.Services.Caching;
using Microsoft.EntityFrameworkCore;

namespace CampaignSystem.Services;

/// <summary>
/// Campaign CRUD and the translation between entity and DTO.
/// The controller never sees an entity, and the database never sees a DTO.
///
/// Takes both the repository and the context on purpose. Single-row work on CAMPAIGN goes
/// through the repository; the context is used directly for the reads that span a campaign's
/// child tables — its condition lines on the way into a DTO, and the reward/enrolment history
/// a delete checks. A campaign's scope lives in <see cref="ICampaignCriteriaService"/> and its
/// terms in <see cref="ICampaignConditionService"/>.
///
/// Every write here can change what the customer campaign list shows, so each evicts the
/// shared <see cref="CampaignCatalog"/> — the next customer request rebuilds it from the
/// database.
/// </summary>
public class CampaignService(
    IRepository<Campaign> repository,
    CampaignDbContext context,
    CampaignCatalogCache catalogCache,
    ICampaignCriteriaService criteriaService,
    ILogger<CampaignService> logger)
    : ICampaignService
{
    public async Task<List<CampaignDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var campaigns = await repository.FindAsync(c => c.IsActive, cancellationToken);
        var ids = campaigns.Select(c => c.Id).ToList();

        var conditionsOf = (await context.CampaignConditions
                .AsNoTracking()
                .Where(x => ids.Contains(x.CampaignId))
                .OrderBy(x => x.DisplayOrder)
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.CampaignId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Text).ToList());

        return campaigns.Select(c => ToDto(c, conditionsOf.GetValueOrDefault(c.Id, []))).ToList();
    }

    public async Task<CampaignDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var campaign = await repository.GetByIdAsync(id);

        if (campaign is null || !campaign.IsActive)
        {
            return null;
        }

        var conditions = await context.CampaignConditions
            .AsNoTracking()
            .Where(x => x.CampaignId == id)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => x.Text)
            .ToListAsync(cancellationToken);

        return ToDto(campaign, conditions);
    }

    public async Task<CampaignDto> CreateAsync(
        CreateCampaignDto dto,
        CancellationToken cancellationToken = default)
    {
        var campaign = new Campaign
        {
            Name = dto.Name,
            Description = dto.Description,
            CampaignType = dto.CampaignType,
            EnrollmentBasis = dto.CampaignType == CampaignType.EnrollmentRequired ? dto.EnrollmentBasis : null,
            EarningType = dto.EarningType,
            Gender = dto.Gender,
            CardType = dto.CardType,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            MinimumAmount = dto.MinimumAmount,
            MaximumAmount = dto.MaximumAmount,
            RewardPoint = dto.RewardPoint,
            MaxRewardAmount = dto.MaxRewardAmount,
            RefundClawbackEnabled = dto.RefundClawbackEnabled,
            RefundClawbackDays = dto.RefundClawbackDays,
            UnusedPointsClawbackEnabled = dto.UnusedPointsClawbackEnabled,
            UnusedPointsClawbackDays = dto.UnusedPointsClawbackDays,

            // The starting status follows from the dates. From here on the daily batch keeps
            // it moving.
            Status = dto.StartDate <= DateTime.Now
                ? CampaignStatus.Ongoing
                : CampaignStatus.Pending,
            IsActive = true
        };

        await repository.AddAsync(campaign, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        catalogCache.Invalidate();

        logger.LogInformation("Campaign {CampaignId} '{Name}' created.", campaign.Id, campaign.Name);

        // Id is filled in by the database during SaveChanges, so the returned DTO carries it.
        // Conditions do not exist yet — nothing has been generated for a campaign this new.
        return ToDto(campaign, []);
    }

    public async Task<bool> UpdateAsync(
        int id,
        UpdateCampaignDto dto,
        CancellationToken cancellationToken = default)
    {
        var campaign = await repository.GetByIdAsync(id);

        if (campaign is null || !campaign.IsActive)
        {
            return false;
        }

        campaign.Name = dto.Name;
        campaign.Description = dto.Description;
        campaign.CampaignType = dto.CampaignType;
        campaign.EnrollmentBasis = dto.CampaignType == CampaignType.EnrollmentRequired ? dto.EnrollmentBasis : null;
        campaign.EarningType = dto.EarningType;
        campaign.Gender = dto.Gender;
        campaign.CardType = dto.CardType;
        campaign.StartDate = dto.StartDate;
        campaign.EndDate = dto.EndDate;
        campaign.MinimumAmount = dto.MinimumAmount;
        campaign.MaximumAmount = dto.MaximumAmount;
        campaign.RewardPoint = dto.RewardPoint;
        campaign.MaxRewardAmount = dto.MaxRewardAmount;
        campaign.RefundClawbackEnabled = dto.RefundClawbackEnabled;
        campaign.RefundClawbackDays = dto.RefundClawbackDays;
        campaign.UnusedPointsClawbackEnabled = dto.UnusedPointsClawbackEnabled;
        campaign.UnusedPointsClawbackDays = dto.UnusedPointsClawbackDays;

        repository.Update(campaign);
        await repository.SaveChangesAsync(cancellationToken);
        catalogCache.Invalidate();

        logger.LogInformation("Campaign {CampaignId} updated.", id);

        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var campaign = await repository.GetByIdAsync(id);

        if (campaign is null || !campaign.IsActive)
        {
            return false;
        }

        // A campaign that has paid a reward or accepted an enrolment carries history worth
        // keeping — and the foreign keys are Restrict, so the database would refuse to remove
        // the row anyway. One that has neither is a mistyped record with nothing to protect,
        // and leaving it behind only clutters the table.
        var hasHistory =
            await context.CampaignRewards.AnyAsync(r => r.CampaignId == id, cancellationToken) ||
            await context.CampaignParticipations.AnyAsync(p => p.CampaignId == id, cancellationToken);

        if (hasHistory)
        {
            campaign.IsActive = false;
            repository.Update(campaign);
        }
        else
        {
            // The child rows point at the campaign, so they go first — nothing else refers
            // to them, and they carry no history of their own.
            await criteriaService.RemoveAllForCampaignAsync(id, cancellationToken);

            repository.Remove(campaign);
        }

        await repository.SaveChangesAsync(cancellationToken);
        catalogCache.Invalidate();

        logger.LogInformation(
            "Campaign {CampaignId} {Action}.", id, hasHistory ? "deactivated" : "deleted");

        return true;
    }

    private static CampaignDto ToDto(Campaign campaign, List<string> conditions) => new()
    {
        Id = campaign.Id,
        Name = campaign.Name,
        Description = campaign.Description,
        CampaignType = campaign.CampaignType,
        EnrollmentBasis = campaign.EnrollmentBasis,
        StartDate = campaign.StartDate,
        EndDate = campaign.EndDate,
        MinimumAmount = campaign.MinimumAmount,
        MaximumAmount = campaign.MaximumAmount,
        RewardPoint = campaign.RewardPoint,
        MaxRewardAmount = campaign.MaxRewardAmount,
        RefundClawbackEnabled = campaign.RefundClawbackEnabled,
        RefundClawbackDays = campaign.RefundClawbackDays,
        UnusedPointsClawbackEnabled = campaign.UnusedPointsClawbackEnabled,
        UnusedPointsClawbackDays = campaign.UnusedPointsClawbackDays,
        EarningType = campaign.EarningType,
        Gender = campaign.Gender,
        CardType = campaign.CardType,
        Status = campaign.Status,
        IsActive = campaign.IsActive,
        Conditions = conditions
    };
}
