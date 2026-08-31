using CampaignSystem.Data;
using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Services.Caching;
using Microsoft.EntityFrameworkCore;

namespace CampaignSystem.Services;

/// <summary>
/// A campaign's scope: the five criteria junction tables that decide who and what a campaign
/// applies to. Reading the scope, replacing it (validated and diffed so only real changes are
/// written), and tearing it down ahead of a hard delete all live here.
///
/// Split out of <see cref="CampaignService"/>: campaign CRUD changes for one set of reasons,
/// the multi-table criteria concern for another. Every write evicts the shared
/// <see cref="CampaignCatalog"/>, since a changed scope changes what the customer list shows.
///
/// Works against the context directly rather than the repository — the criteria live across
/// five tables written in one transaction, which does not fit behind IRepository.
/// </summary>
public class CampaignCriteriaService(
    CampaignDbContext context,
    CampaignCatalogCache catalogCache,
    ILogger<CampaignCriteriaService> logger)
    : ICampaignCriteriaService
{
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
                .ToListAsync(cancellationToken),

            ClawbackExemptProductIds = await context.CampaignClawbackExemptProducts
                .Where(x => x.CampaignId == campaignId)
                .Select(x => x.ProductId)
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
        var clawbackExemptProductIds = dto.ClawbackExemptProductIds.Distinct().ToList();

        var error = await FindUnknownReferencesAsync(
            segmentIds, productIds, merchantIds, transactionCodeIds, clawbackExemptProductIds, cancellationToken);

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

        await SyncAsync(
            context.CampaignClawbackExemptProducts,
            campaignId,
            clawbackExemptProductIds,
            x => x.ProductId,
            productId => new CampaignClawbackExemptProduct { CampaignId = campaignId, ProductId = productId },
            cancellationToken);

        // One SaveChanges for all five tables, so the campaign never sits with half of its
        // new scope applied.
        await context.SaveChangesAsync(cancellationToken);
        catalogCache.Invalidate();

        logger.LogInformation("Campaign {CampaignId} criteria updated.", campaignId);

        return SetCriteriaOutcome.Success();
    }

    public async Task RemoveAllForCampaignAsync(int campaignId, CancellationToken cancellationToken = default)
    {
        context.CampaignSegments.RemoveRange(
            await context.CampaignSegments.Where(x => x.CampaignId == campaignId).ToListAsync(cancellationToken));

        context.CampaignProducts.RemoveRange(
            await context.CampaignProducts.Where(x => x.CampaignId == campaignId).ToListAsync(cancellationToken));

        context.CampaignMerchants.RemoveRange(
            await context.CampaignMerchants.Where(x => x.CampaignId == campaignId).ToListAsync(cancellationToken));

        context.CampaignTransactionCodes.RemoveRange(
            await context.CampaignTransactionCodes.Where(x => x.CampaignId == campaignId).ToListAsync(cancellationToken));

        context.CampaignClawbackExemptProducts.RemoveRange(
            await context.CampaignClawbackExemptProducts.Where(x => x.CampaignId == campaignId).ToListAsync(cancellationToken));

        context.CampaignConditions.RemoveRange(
            await context.CampaignConditions.Where(x => x.CampaignId == campaignId).ToListAsync(cancellationToken));
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
        List<int> clawbackExemptProductIds,
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

        Collect(clawbackExemptProductIds, await context.Products
            .Where(x => clawbackExemptProductIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(cancellationToken), "clawback-exempt product");

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
}
