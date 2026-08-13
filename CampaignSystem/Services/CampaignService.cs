using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Enums;
using CampaignSystem.Repositories;

namespace CampaignSystem.Services;

/// <summary>
/// Campaign business rules and the translation between entity and DTO.
/// The controller never sees an entity, and the database never sees a DTO.
/// </summary>
public class CampaignService(IRepository<Campaign> repository) : ICampaignService
{
    public async Task<List<CampaignDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var campaigns = await repository.FindAsync(c => c.IsActive, cancellationToken);
        return campaigns.Select(ToDto).ToList();
    }

    public async Task<CampaignDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var campaign = await repository.GetByIdAsync(id);
        return campaign is null || !campaign.IsActive ? null : ToDto(campaign);
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
            EarningType = dto.EarningType,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            MinimumAmount = dto.MinimumAmount,
            MaximumAmount = dto.MaximumAmount,
            RewardPoint = dto.RewardPoint,
            MaxRewardAmount = dto.MaxRewardAmount,

            // Decided here, never by the caller.
            Status = CampaignStatus.Draft,
            IsActive = true
        };

        await repository.AddAsync(campaign, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        // Id is filled in by the database during SaveChanges, so the returned DTO carries it.
        return ToDto(campaign);
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
        campaign.EarningType = dto.EarningType;
        campaign.StartDate = dto.StartDate;
        campaign.EndDate = dto.EndDate;
        campaign.MinimumAmount = dto.MinimumAmount;
        campaign.MaximumAmount = dto.MaximumAmount;
        campaign.RewardPoint = dto.RewardPoint;
        campaign.MaxRewardAmount = dto.MaxRewardAmount;

        repository.Update(campaign);
        await repository.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var campaign = await repository.GetByIdAsync(id);

        if (campaign is null || !campaign.IsActive)
        {
            return false;
        }

        // Soft delete. The row stays, and so do the rewards and enrollments that point at it.
        campaign.IsActive = false;

        repository.Update(campaign);
        await repository.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static CampaignDto ToDto(Campaign campaign) => new()
    {
        Id = campaign.Id,
        Name = campaign.Name,
        Description = campaign.Description,
        CampaignType = campaign.CampaignType,
        StartDate = campaign.StartDate,
        EndDate = campaign.EndDate,
        MinimumAmount = campaign.MinimumAmount,
        MaximumAmount = campaign.MaximumAmount,
        RewardPoint = campaign.RewardPoint,
        MaxRewardAmount = campaign.MaxRewardAmount,
        EarningType = campaign.EarningType,
        Status = campaign.Status,
        IsActive = campaign.IsActive
    };
}
