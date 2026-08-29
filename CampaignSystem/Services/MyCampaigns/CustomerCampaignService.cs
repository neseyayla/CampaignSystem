using CampaignSystem.Data;
using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Enums;
using CampaignSystem.Services.Caching;
using Microsoft.EntityFrameworkCore;

namespace CampaignSystem.Services;

/// <summary>
/// Which campaigns a given customer could earn from, and their standing in them.
///
/// The one rule written here is eligibility. Enrollment and the progress figure are
/// delegated to the services that already own them, so a customer signing up from their
/// own screen and a branch signing them up over the counter go through identical checks.
///
/// Works against the context directly: the criteria live in four junction tables and are
/// read for every open campaign at once, which is not what a repository is for. That
/// person-independent half — the open campaigns and their criteria and terms — is the same
/// for every customer and rarely changes, so it is built once into a <see cref="CampaignCatalog"/>
/// and cached; only the per-customer part (eligibility and enrolment) runs each request.
/// </summary>
public class CustomerCampaignService(
    CampaignDbContext context,
    IParticipationService participationService,
    IRewardService rewardService,
    CampaignCatalogCache catalogCache) : ICustomerCampaignService
{
    public async Task<List<CustomerCampaignDto>?> GetEligibleAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        var customer = await LoadCustomerAsync(customerId, cancellationToken);

        return customer is null ? null : await BuildAsync(customer, cancellationToken);
    }

    public async Task<ServiceResult<CustomerCampaignDetailDto>> GetOneAsync(
        int customerId,
        int campaignId,
        CancellationToken cancellationToken = default)
    {
        var eligible = await GetEligibleAsync(customerId, cancellationToken);

        var campaign = eligible?.FirstOrDefault(c => c.CampaignId == campaignId);

        if (campaign is null)
        {
            return ServiceResult<CustomerCampaignDetailDto>.NotFound();
        }

        var preview = await rewardService.PreviewAsync(campaignId, customerId, cancellationToken);

        if (preview.Status != ResultStatus.Success)
        {
            // The campaign was in the eligible list a moment ago and the customer was
            // active enough to produce it, so only a record changing underneath us gets
            // here. Reported as absent rather than as an error, which is what it is.
            return ServiceResult<CustomerCampaignDetailDto>.NotFound();
        }

        return ServiceResult<CustomerCampaignDetailDto>.Success(new CustomerCampaignDetailDto
        {
            Campaign = campaign,
            Progress = preview.Value!
        });
    }

    public async Task<ServiceResult<ParticipationDto>> EnrollAsync(
        int customerId,
        int campaignId,
        int? cardId,
        CancellationToken cancellationToken = default)
    {
        var eligible = await GetEligibleAsync(customerId, cancellationToken);

        if (eligible is null || eligible.All(c => c.CampaignId != campaignId))
        {
            return ServiceResult<ParticipationDto>.NotFound();
        }

        // Everything from here on — that the campaign takes enrollments at all, that it is
        // still open to them, that the card belongs to this customer, that they have not
        // already signed up — belongs to the enrollment service and is not repeated.
        return await participationService.EnrollAsync(
            campaignId,
            new CreateParticipationDto { CustomerId = customerId, CardId = cardId },
            cancellationToken);
    }

    /// <summary>
    /// The customer with the cards they can actually spend on. Closed cards are left out:
    /// one cannot produce a transaction, so it must not make its holder look eligible for
    /// a campaign restricted to the product it carries.
    /// </summary>
    private async Task<Customer?> LoadCustomerAsync(int customerId, CancellationToken cancellationToken)
        => await context.Customers
            .AsNoTracking()
            .Include(c => c.Cards.Where(card => card.IsActive))
            .FirstOrDefaultAsync(c => c.Id == customerId && c.IsActive, cancellationToken);

    private async Task<List<CustomerCampaignDto>> BuildAsync(
        Customer customer,
        CancellationToken cancellationToken)
    {
        var catalog = await catalogCache.GetOrBuildAsync(() => BuildCatalogAsync(cancellationToken));

        if (catalog.Candidates.Count == 0)
        {
            return [];
        }

        var ids = catalog.Candidates.Select(c => c.Id).ToList();

        // The one query that stays per customer: which of these campaigns this customer has
        // already joined. Everything else the list shows is the same for everyone and comes
        // from the cached catalog.
        var enrolledIn = (await context.CampaignParticipations
                .AsNoTracking()
                .Where(p => p.CustomerId == customer.Id
                            && p.Status == ParticipationStatus.Active
                            && ids.Contains(p.CampaignId))
                .Select(p => p.CampaignId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var now = DateTime.Now;

        return catalog.Candidates
            .Where(c => IsEligible(
                c,
                customer,
                catalog.SegmentsByCampaign.GetValueOrDefault(c.Id, []),
                catalog.ProductsByCampaign.GetValueOrDefault(c.Id, [])))
            .Select(c => new CustomerCampaignDto
            {
                CampaignId = c.Id,
                Name = c.Name,
                Description = c.Description,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                HasStarted = c.StartDate <= now,
                EnrollmentRequired = c.CampaignType == CampaignType.EnrollmentRequired,
                RewardPending = c.Status == CampaignStatus.Loading,
                Enrolled = enrolledIn.Contains(c.Id),
                EarningType = c.EarningType,
                RewardPoint = c.RewardPoint,
                MaxRewardAmount = c.MaxRewardAmount,
                MinimumAmount = c.MinimumAmount,
                MaximumAmount = c.MaximumAmount,
                Merchants = catalog.MerchantsByCampaign.GetValueOrDefault(c.Id, []),
                TransactionCodes = catalog.CodesByCampaign.GetValueOrDefault(c.Id, []),
                Conditions = catalog.ConditionsByCampaign.GetValueOrDefault(c.Id, [])
            })
            .ToList();
    }

    /// <summary>
    /// The person-independent catalog: the open campaigns with their criteria and terms,
    /// read once and cached. Nothing here depends on who is asking — the caller layers
    /// eligibility and enrolment on top per request.
    /// </summary>
    private async Task<CampaignCatalog> BuildCatalogAsync(CancellationToken cancellationToken)
    {
        // Pending campaigns are listed alongside running ones. One that has not begun is
        // still worth reading about, and an enrollment campaign can be joined before day
        // one — which is precisely when joining is worth the most. Loading ones are kept too:
        // a campaign that has ended but not yet paid out (the batch loads rewards up to a few
        // days later) would otherwise vanish from a customer who took part, so it stays on with
        // a "reward being calculated" note. Only Ended is left out — its points are on the
        // rewards endpoint by then.
        var candidates = await context.Campaigns
            .AsNoTracking()
            .Where(c => c.IsActive &&
                        (c.Status == CampaignStatus.Ongoing
                         || c.Status == CampaignStatus.Pending
                         || c.Status == CampaignStatus.Loading))
            .OrderBy(c => c.StartDate)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return new CampaignCatalog(
                candidates,
                new Dictionary<int, HashSet<int>>(),
                new Dictionary<int, HashSet<int>>(),
                new Dictionary<int, List<string>>(),
                new Dictionary<int, List<string>>(),
                new Dictionary<int, List<string>>());
        }

        var ids = candidates.Select(c => c.Id).ToList();

        // Four queries for every open campaign rather than four per campaign. The rows are
        // grouped afterwards in memory because EF translates a GroupBy that aggregates,
        // but not one that hands back the members of each group.
        var segmentRows = await context.CampaignSegments
            .AsNoTracking()
            .Where(x => ids.Contains(x.CampaignId))
            .Select(x => new { x.CampaignId, x.SegmentId })
            .ToListAsync(cancellationToken);

        var productRows = await context.CampaignProducts
            .AsNoTracking()
            .Where(x => ids.Contains(x.CampaignId))
            .Select(x => new { x.CampaignId, x.ProductId })
            .ToListAsync(cancellationToken);

        // Merchants and transaction types come back as names. They never narrow who the
        // campaign is for, only where and how the spending counts, so the customer reads
        // them as terms rather than being filtered by them.
        var merchantRows = await context.CampaignMerchants
            .AsNoTracking()
            .Where(x => ids.Contains(x.CampaignId))
            .Select(x => new { x.CampaignId, Name = x.Merchant.MerchantName })
            .ToListAsync(cancellationToken);

        var codeRows = await context.CampaignTransactionCodes
            .AsNoTracking()
            .Where(x => ids.Contains(x.CampaignId))
            .Select(x => new { x.CampaignId, x.TransactionCode.Name })
            .ToListAsync(cancellationToken);

        // Written once by an operator rather than derived here, so what the customer reads
        // is exactly what was reviewed — not a live re-guess at every request.
        var conditionRows = await context.CampaignConditions
            .AsNoTracking()
            .Where(x => ids.Contains(x.CampaignId))
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new { x.CampaignId, x.Text })
            .ToListAsync(cancellationToken);

        var segmentsOf = segmentRows.GroupBy(x => x.CampaignId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.SegmentId).ToHashSet());

        var productsOf = productRows.GroupBy(x => x.CampaignId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ProductId).ToHashSet());

        var merchantsOf = merchantRows.GroupBy(x => x.CampaignId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name).OrderBy(n => n).ToList());

        var codesOf = codeRows.GroupBy(x => x.CampaignId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name).OrderBy(n => n).ToList());

        var conditionsOf = conditionRows.GroupBy(x => x.CampaignId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Text).ToList());

        return new CampaignCatalog(
            candidates, segmentsOf, productsOf, merchantsOf, codesOf, conditionsOf);
    }

    /// <summary>
    /// Whether any transaction of this customer could ever qualify for the campaign.
    ///
    /// Only the criteria that describe the person are weighed. Merchant, transaction type
    /// and amount describe the spending instead: they decide which of a customer's
    /// transactions count, never whether the customer belongs in the campaign, so
    /// filtering on them here would hide campaigns that are wide open to them.
    /// </summary>
    private static bool IsEligible(
        Campaign campaign,
        Customer customer,
        HashSet<int> segmentIds,
        HashSet<int> productIds)
    {
        // The same reading as everywhere else in the system: a criterion the campaign says
        // nothing about is the absence of a restriction, not a filter that excludes all.
        if (segmentIds.Count > 0 &&
            (customer.SegmentId is null || !segmentIds.Contains(customer.SegmentId.Value)))
        {
            return false;
        }

        if (campaign.Gender is not null && customer.Gender != campaign.Gender)
        {
            return false;
        }

        // Product and card type are two conditions on one card, not two conditions on the
        // customer. Someone holding a Platinum primary card and a Classic supplementary
        // card satisfies "Platinum" and satisfies "supplementary" separately, yet holds no
        // card that satisfies both — and it is a card, not a customer, that produces the
        // transaction a reward is paid on.
        if (productIds.Count > 0 || campaign.CardType is not null)
        {
            return customer.Cards.Any(card =>
                (productIds.Count == 0 || productIds.Contains(card.ProductId)) &&
                (campaign.CardType is null || card.CardType == campaign.CardType));
        }

        // A campaign open to every card still needs the customer to hold one.
        return customer.Cards.Count > 0;
    }
}
