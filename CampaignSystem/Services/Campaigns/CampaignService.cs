using CampaignSystem.Data;
using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Enums;
using CampaignSystem.Repositories;
using CampaignSystem.Services.Caching;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CampaignSystem.Services;

/// <summary>
/// Campaign business rules and the translation between entity and DTO.
/// The controller never sees an entity, and the database never sees a DTO.
///
/// Takes both the repository and the context on purpose. Single-row work on CAMPAIGN goes
/// through the repository; the criteria methods touch five tables in one transaction and
/// need the context directly, which the repository deliberately does not expose.
///
/// Every write here can change what the customer campaign list shows, so each evicts the
/// shared <see cref="CampaignCatalog"/> — the next customer request rebuilds it from the
/// database.
/// </summary>
public class CampaignService(
    IRepository<Campaign> repository,
    CampaignDbContext context,
    CampaignCatalogCache catalogCache)
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

        repository.Update(campaign);
        await repository.SaveChangesAsync(cancellationToken);
        catalogCache.Invalidate();

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
        catalogCache.Invalidate();

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

        context.CampaignConditions.RemoveRange(
            await context.CampaignConditions.Where(x => x.CampaignId == campaignId).ToListAsync(cancellationToken));
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
        catalogCache.Invalidate();

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
        EarningType = campaign.EarningType,
        Gender = campaign.Gender,
        CardType = campaign.CardType,
        Status = campaign.Status,
        IsActive = campaign.IsActive,
        Conditions = conditions
    };

    // Conditions ---------------------------------------------------------------

    public async Task<List<CampaignConditionDto>?> GetConditionsAsync(
        int campaignId,
        CancellationToken cancellationToken = default)
    {
        var campaignExists = await context.Campaigns
            .AnyAsync(c => c.Id == campaignId && c.IsActive, cancellationToken);

        return campaignExists ? await LoadConditionsAsync(campaignId, cancellationToken) : null;
    }

    public async Task<bool> SetConditionsAsync(
        int campaignId,
        List<CampaignConditionDto> conditions,
        CancellationToken cancellationToken = default)
    {
        var campaignExists = await context.Campaigns
            .AnyAsync(c => c.Id == campaignId && c.IsActive, cancellationToken);

        if (!campaignExists)
        {
            return false;
        }

        var existing = await context.CampaignConditions
            .Where(x => x.CampaignId == campaignId)
            .ToListAsync(cancellationToken);

        context.CampaignConditions.RemoveRange(existing);

        context.CampaignConditions.AddRange(conditions.Select((c, index) => new CampaignCondition
        {
            CampaignId = campaignId,
            Text = c.Text,
            DisplayOrder = index,
            IsAutoGenerated = c.IsAutoGenerated
        }));

        await context.SaveChangesAsync(cancellationToken);
        catalogCache.Invalidate();

        return true;
    }

    public async Task<List<CampaignConditionDto>?> GenerateConditionsAsync(
        int campaignId,
        CancellationToken cancellationToken = default)
    {
        var campaign = await context.Campaigns
            .FirstOrDefaultAsync(c => c.Id == campaignId && c.IsActive, cancellationToken);

        if (campaign is null)
        {
            return null;
        }

        var generatedTexts = await BuildAutoConditionTextsAsync(campaign, cancellationToken);

        // Only the rows the previous run generated are replaced. A line an operator wrote
        // by hand is not this method's to touch.
        var previousAuto = await context.CampaignConditions
            .Where(x => x.CampaignId == campaignId && x.IsAutoGenerated)
            .ToListAsync(cancellationToken);

        context.CampaignConditions.RemoveRange(previousAuto);

        context.CampaignConditions.AddRange(generatedTexts.Select((text, index) => new CampaignCondition
        {
            CampaignId = campaignId,
            Text = text,
            DisplayOrder = index,
            IsAutoGenerated = true
        }));

        // Hand-written lines always read after the generated ones, in the order they were
        // added — renumbered here since the generated block above may have grown or shrunk.
        var manual = await context.CampaignConditions
            .Where(x => x.CampaignId == campaignId && !x.IsAutoGenerated)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(cancellationToken);

        for (var i = 0; i < manual.Count; i++)
        {
            manual[i].DisplayOrder = generatedTexts.Count + i;
        }

        await context.SaveChangesAsync(cancellationToken);
        catalogCache.Invalidate();

        return await LoadConditionsAsync(campaignId, cancellationToken);
    }

    private async Task<List<CampaignConditionDto>> LoadConditionsAsync(
        int campaignId,
        CancellationToken cancellationToken)
        => await context.CampaignConditions
            .AsNoTracking()
            .Where(x => x.CampaignId == campaignId)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new CampaignConditionDto
            {
                Id = x.Id,
                Text = x.Text,
                DisplayOrder = x.DisplayOrder,
                IsAutoGenerated = x.IsAutoGenerated
            })
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Matches a <c>{TokenName}</c> placeholder inside a <see cref="CampaignConditionTemplate"/>.
    /// </summary>
    private static readonly Regex ConditionTemplateTokenPattern = new(@"\{(\w+)\}", RegexOptions.Compiled);

    /// <summary>
    /// Fills a template's <c>{TokenName}</c> placeholders from <paramref name="tokens"/>.
    /// A placeholder with no matching entry throws rather than being left in the text
    /// verbatim, since that only happens when a template row was hand-edited into a token
    /// the code never sends — better to surface it than show it to a customer.
    /// </summary>
    private static string RenderConditionTemplate(string templateText, IReadOnlyDictionary<string, string> tokens)
        => ConditionTemplateTokenPattern.Replace(templateText, match =>
        {
            var token = match.Groups[1].Value;

            if (!tokens.TryGetValue(token, out var value))
            {
                throw new InvalidOperationException(
                    $"Campaign condition template references unknown token '{{{token}}}'.");
            }

            return value;
        });

    /// <summary>
    /// Turns a campaign's rules and criteria into the sentences an operator would otherwise
    /// have to type by hand. Reads the same fields <see cref="RewardService"/> qualifies
    /// transactions on, so the wording never claims a restriction the reward engine does
    /// not actually enforce.
    ///
    /// The sentences themselves live in <see cref="CampaignConditionTemplate"/>, keyed by the
    /// scenario below that decided the line applies — that decision stays in code, only the
    /// wording is data. A key with no active row (deleted or turned off with
    /// <see cref="CampaignConditionTemplate.IsActive"/>) simply contributes no line.
    /// </summary>
    private async Task<List<string>> BuildAutoConditionTextsAsync(
        Campaign campaign,
        CancellationToken cancellationToken)
    {
        var templates = await context.CampaignConditionTemplates
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToDictionaryAsync(x => x.Key, cancellationToken);

        var segmentNames = await context.CampaignSegments
            .Where(x => x.CampaignId == campaign.Id)
            .Select(x => x.Segment.SegmentName)
            .ToListAsync(cancellationToken);

        var productNames = await context.CampaignProducts
            .Where(x => x.CampaignId == campaign.Id)
            .Select(x => x.Product.ProductName)
            .ToListAsync(cancellationToken);

        var merchantNames = await context.CampaignMerchants
            .Where(x => x.CampaignId == campaign.Id)
            .Select(x => x.Merchant.MerchantName)
            .ToListAsync(cancellationToken);

        var transactionCodeNames = await context.CampaignTransactionCodes
            .Where(x => x.CampaignId == campaign.Id)
            .Select(x => x.TransactionCode.Name)
            .ToListAsync(cancellationToken);

        return BuildAutoConditionTexts(
            templates,
            campaign.CampaignType,
            campaign.StartDate,
            campaign.EndDate,
            campaign.MinimumAmount,
            campaign.MaximumAmount,
            campaign.RewardPoint,
            campaign.MaxRewardAmount,
            campaign.AccumulatesPerCard,
            campaign.Gender,
            campaign.CardType,
            segmentNames,
            productNames,
            merchantNames,
            transactionCodeNames);
    }

    public async Task<List<string>> PreviewConditionsAsync(
        CampaignConditionsPreviewDto dto,
        CancellationToken cancellationToken = default)
    {
        var templates = await context.CampaignConditionTemplates
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToDictionaryAsync(x => x.Key, cancellationToken);

        // Unlike the persisted flow, nothing has been saved yet — the campaign's scope is
        // still just a handful of ids the operator picked, so the names are looked up
        // directly rather than through the CampaignSegments/CampaignProducts/... junction
        // tables (which only exist once a campaign row does).
        var segmentNames = await context.Segments
            .Where(x => dto.Criteria.SegmentIds.Contains(x.Id))
            .Select(x => x.SegmentName)
            .ToListAsync(cancellationToken);

        var productNames = await context.Products
            .Where(x => dto.Criteria.ProductIds.Contains(x.Id))
            .Select(x => x.ProductName)
            .ToListAsync(cancellationToken);

        var merchantNames = await context.Merchants
            .Where(x => dto.Criteria.MerchantIds.Contains(x.Id))
            .Select(x => x.MerchantName)
            .ToListAsync(cancellationToken);

        var transactionCodeNames = await context.TransactionCodes
            .Where(x => dto.Criteria.TransactionCodeIds.Contains(x.Id))
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        return BuildAutoConditionTexts(
            templates,
            dto.CampaignType,
            dto.StartDate,
            dto.EndDate,
            dto.MinimumAmount,
            dto.MaximumAmount,
            dto.RewardPoint,
            dto.MaxRewardAmount,
            dto.EarningType == EarningType.CardBased,
            dto.Gender,
            dto.CardType,
            segmentNames,
            productNames,
            merchantNames,
            transactionCodeNames);
    }

    /// <summary>
    /// The template-rendering core both <see cref="BuildAutoConditionTextsAsync"/> (a saved
    /// campaign) and <see cref="PreviewConditionsAsync"/> (a draft that is not saved yet)
    /// reduce to before calling this. Takes plain values rather than a <see cref="Campaign"/>
    /// so a draft with no database row can produce exactly the same sentences a saved one
    /// would.
    /// </summary>
    private static List<string> BuildAutoConditionTexts(
        Dictionary<string, CampaignConditionTemplate> templates,
        CampaignType campaignType,
        DateTime? startDate,
        DateTime? endDate,
        decimal? minimumAmount,
        decimal? maximumAmount,
        decimal? rewardPoint,
        decimal? maxRewardAmount,
        bool accumulatesPerCard,
        Gender? gender,
        CardType? cardType,
        List<string> segmentNames,
        List<string> productNames,
        List<string> merchantNames,
        List<string> transactionCodeNames)
    {
        var lines = new List<string>();

        void AddLine(string key, Dictionary<string, string> tokens)
        {
            if (templates.TryGetValue(key, out var template))
            {
                lines.Add(RenderConditionTemplate(template.TemplateText, tokens));
            }
        }

        // A draft with no dates yet has nothing to say here — this only happens on the
        // preview path, since a saved campaign always has both.
        if (startDate is not null && endDate is not null)
        {
            AddLine("DateRange", new Dictionary<string, string>
            {
                ["StartDate"] = startDate.Value.ToString("dd.MM.yyyy"),
                ["EndDate"] = endDate.Value.ToString("dd.MM.yyyy")
            });
        }

        if (campaignType == CampaignType.EnrollmentRequired)
        {
            AddLine("EnrollmentRequired", []);
        }

        if (minimumAmount is not null && maximumAmount is not null)
        {
            AddLine("MinAndMaxAmount", new Dictionary<string, string>
            {
                ["MinimumAmount"] = minimumAmount.Value.ToString("N0"),
                ["MaximumAmount"] = maximumAmount.Value.ToString("N0")
            });
        }
        else if (minimumAmount is not null)
        {
            AddLine("MinAmountOnly", new Dictionary<string, string>
            {
                ["MinimumAmount"] = minimumAmount.Value.ToString("N0")
            });
        }
        else if (maximumAmount is not null)
        {
            AddLine("MaxAmountOnly", new Dictionary<string, string>
            {
                ["MaximumAmount"] = maximumAmount.Value.ToString("N0")
            });
        }

        if (rewardPoint is not null)
        {
            AddLine("RewardPoint", new Dictionary<string, string>
            {
                ["RewardPoint"] = rewardPoint.Value.ToString("N0")
            });
        }

        if (maxRewardAmount is not null)
        {
            var perUnit = accumulatesPerCard ? "kart" : "müşteri";
            AddLine("MaxRewardAmount", new Dictionary<string, string>
            {
                ["PerUnit"] = perUnit,
                ["MaxRewardAmount"] = maxRewardAmount.Value.ToString("N0")
            });
        }

        if (gender is not null)
        {
            var genderText = gender == Gender.Female ? "kadın" : "erkek";
            AddLine("Gender", new Dictionary<string, string> { ["GenderText"] = genderText });
        }

        if (cardType is not null)
        {
            var cardTypeText = cardType == CardType.Primary ? "asıl" : "ek";
            AddLine("CardType", new Dictionary<string, string> { ["CardTypeText"] = cardTypeText });
        }

        if (segmentNames.Count > 0)
        {
            AddLine("SegmentList", new Dictionary<string, string>
            {
                ["Names"] = string.Join(", ", segmentNames.OrderBy(n => n))
            });
        }

        if (productNames.Count > 0)
        {
            AddLine("ProductList", new Dictionary<string, string>
            {
                ["Names"] = string.Join(", ", productNames.OrderBy(n => n))
            });
        }

        if (merchantNames.Count > 0)
        {
            AddLine("MerchantList", new Dictionary<string, string>
            {
                ["Names"] = string.Join(", ", merchantNames.OrderBy(n => n))
            });
        }

        if (transactionCodeNames.Count > 0)
        {
            AddLine("TransactionCodeList", new Dictionary<string, string>
            {
                ["Names"] = string.Join(", ", transactionCodeNames.OrderBy(n => n))
            });
        }

        return lines;
    }
}
