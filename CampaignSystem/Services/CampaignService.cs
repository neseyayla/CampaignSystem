using CampaignSystem.Data;
using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Enums;
using CampaignSystem.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CampaignSystem.Services;

/// <summary>
/// Campaign business rules and the translation between entity and DTO.
/// The controller never sees an entity, and the database never sees a DTO.
///
/// Takes both the repository and the context on purpose. Single-row work on CAMPAIGN goes
/// through the repository; the criteria methods touch five tables in one transaction and
/// need the context directly, which the repository deliberately does not expose.
/// </summary>
public class CampaignService(IRepository<Campaign> repository, CampaignDbContext context)
    : ICampaignService
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
            Gender = dto.Gender,
            CardType = dto.CardType,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            MinimumAmount = dto.MinimumAmount,
            MaximumAmount = dto.MaximumAmount,
            RewardPoint = dto.RewardPoint,
            MaxRewardAmount = dto.MaxRewardAmount,

            // The starting status follows from the dates. From here on the daily batch keeps
            // it moving.
            Status = dto.StartDate <= DateTime.Now
                ? CampaignStatus.Ongoing
                : CampaignStatus.Pending,
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
        campaign.Gender = dto.Gender;
        campaign.CardType = dto.CardType;
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
            // The criteria rows point at the campaign, so they go first — nothing else refers
            // to them, and they carry no history of their own.
            await RemoveCriteriaAsync(id, cancellationToken);

            repository.Remove(campaign);
        }

        await repository.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>Clears every criteria row of a campaign that is about to be removed outright.</summary>
    private async Task RemoveCriteriaAsync(int campaignId, CancellationToken cancellationToken)
    {
        context.CampaignSegments.RemoveRange(
            await context.CampaignSegments.Where(x => x.CampaignId == campaignId).ToListAsync(cancellationToken));

        context.CampaignProducts.RemoveRange(
            await context.CampaignProducts.Where(x => x.CampaignId == campaignId).ToListAsync(cancellationToken));

        context.CampaignMerchants.RemoveRange(
            await context.CampaignMerchants.Where(x => x.CampaignId == campaignId).ToListAsync(cancellationToken));

        context.CampaignTransactionCodes.RemoveRange(
            await context.CampaignTransactionCodes.Where(x => x.CampaignId == campaignId).ToListAsync(cancellationToken));
    }

    public async Task<CampaignCriteriaDto?> GetCriteriaAsync(
        int campaignId,
        CancellationToken cancellationToken = default)
    {
        var campaignExists = await context.Campaigns
            .AnyAsync(c => c.Id == campaignId && c.IsActive, cancellationToken);

        if (!campaignExists)
        {
            return null;
        }

        return new CampaignCriteriaDto
        {
            SegmentIds = await context.CampaignSegments
                .Where(x => x.CampaignId == campaignId)
                .Select(x => x.SegmentId)
                .ToListAsync(cancellationToken),

            ProductIds = await context.CampaignProducts
                .Where(x => x.CampaignId == campaignId)
                .Select(x => x.ProductId)
                .ToListAsync(cancellationToken),

            MerchantIds = await context.CampaignMerchants
                .Where(x => x.CampaignId == campaignId)
                .Select(x => x.MerchantId)
                .ToListAsync(cancellationToken),

            TransactionCodeIds = await context.CampaignTransactionCodes
                .Where(x => x.CampaignId == campaignId)
                .Select(x => x.TransactionCodeId)
                .ToListAsync(cancellationToken)
        };
    }

    public async Task<SetCriteriaOutcome> SetCriteriaAsync(
        int campaignId,
        CampaignCriteriaDto dto,
        CancellationToken cancellationToken = default)
    {
        var campaignExists = await context.Campaigns
            .AnyAsync(c => c.Id == campaignId && c.IsActive, cancellationToken);

        if (!campaignExists)
        {
            return SetCriteriaOutcome.CampaignNotFound();
        }

        // A repeated id in the request is the caller's slip, not a reason to fail.
        var segmentIds = dto.SegmentIds.Distinct().ToList();
        var productIds = dto.ProductIds.Distinct().ToList();
        var merchantIds = dto.MerchantIds.Distinct().ToList();
        var transactionCodeIds = dto.TransactionCodeIds.Distinct().ToList();

        var error = await FindUnknownReferencesAsync(
            segmentIds, productIds, merchantIds, transactionCodeIds, cancellationToken);

        if (error is not null)
        {
            return SetCriteriaOutcome.InvalidReference(error);
        }

        await SyncAsync(
            context.CampaignSegments,
            campaignId,
            segmentIds,
            x => x.SegmentId,
            segmentId => new CampaignSegment { CampaignId = campaignId, SegmentId = segmentId },
            cancellationToken);

        await SyncAsync(
            context.CampaignProducts,
            campaignId,
            productIds,
            x => x.ProductId,
            productId => new CampaignProduct { CampaignId = campaignId, ProductId = productId },
            cancellationToken);

        await SyncAsync(
            context.CampaignMerchants,
            campaignId,
            merchantIds,
            x => x.MerchantId,
            merchantId => new CampaignMerchant { CampaignId = campaignId, MerchantId = merchantId },
            cancellationToken);

        await SyncAsync(
            context.CampaignTransactionCodes,
            campaignId,
            transactionCodeIds,
            x => x.TransactionCodeId,
            transactionCodeId => new CampaignTransactionCode
            {
                CampaignId = campaignId,
                TransactionCodeId = transactionCodeId
            },
            cancellationToken);

        // One SaveChanges for all four tables, so the campaign never sits with half of its
        // new scope applied.
        await context.SaveChangesAsync(cancellationToken);

        return SetCriteriaOutcome.Success();
    }

    /// <summary>
    /// Brings one criteria table in line with the requested ids.
    ///
    /// Only the real difference is written: rows that should stay are left untouched.
    /// Deleting every row and re-inserting the same ids would make EF track a removed and
    /// an added entity under the same composite key, which it rejects.
    /// </summary>
    private async Task SyncAsync<TJunction>(
        DbSet<TJunction> table,
        int campaignId,
        List<int> requestedIds,
        Func<TJunction, int> referenceIdOf,
        Func<int, TJunction> create,
        CancellationToken cancellationToken)
        where TJunction : class
    {
        var existing = await table
            .Where(x => EF.Property<int>(x, "CampaignId") == campaignId)
            .ToListAsync(cancellationToken);

        var existingIds = existing.Select(referenceIdOf).ToHashSet();

        table.RemoveRange(existing.Where(x => !requestedIds.Contains(referenceIdOf(x))));
        table.AddRange(requestedIds.Where(id => !existingIds.Contains(id)).Select(create));
    }

    /// <summary>
    /// Reports every id that does not exist, rather than failing on the first one, so the
    /// caller can correct the whole request in one go.
    /// </summary>
    private async Task<string?> FindUnknownReferencesAsync(
        List<int> segmentIds,
        List<int> productIds,
        List<int> merchantIds,
        List<int> transactionCodeIds,
        CancellationToken cancellationToken)
    {
        var problems = new List<string>();

        Collect(segmentIds, await context.Segments
            .Where(x => segmentIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(cancellationToken), "segment");

        Collect(productIds, await context.Products
            .Where(x => productIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(cancellationToken), "product");

        Collect(merchantIds, await context.Merchants
            .Where(x => merchantIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(cancellationToken), "merchant");

        Collect(transactionCodeIds, await context.TransactionCodes
            .Where(x => transactionCodeIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(cancellationToken), "transaction code");

        return problems.Count == 0 ? null : string.Join(" ", problems);

        void Collect(List<int> requested, List<int> found, string label)
        {
            var missing = requested.Except(found).ToList();

            if (missing.Count > 0)
            {
                problems.Add($"Unknown {label} ids: {string.Join(", ", missing)}.");
            }
        }
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
        Gender = campaign.Gender,
        CardType = campaign.CardType,
        Status = campaign.Status,
        IsActive = campaign.IsActive
    };
}
