using CampaignSystem.Entities;

namespace CampaignSystem.Services.Caching;

/// <summary>
/// The person-independent half of the customer campaign list: the open campaigns and their
/// criteria and terms, exactly as they read for every customer. What is left out is the part
/// that differs per customer — eligibility, enrolment, the progress figure — which the
/// service applies on top of this each request.
///
/// Cached as one object through <see cref="CampaignCatalogCache"/>. The campaigns are the
/// detached, no-tracking entities the build read; only their scalar fields are ever touched,
/// so they are safe to hand to a later request whose own DbContext has since been disposed.
/// </summary>
public sealed record CampaignCatalog(
    IReadOnlyList<Campaign> Candidates,
    IReadOnlyDictionary<int, HashSet<int>> SegmentsByCampaign,
    IReadOnlyDictionary<int, HashSet<int>> ProductsByCampaign,
    IReadOnlyDictionary<int, List<string>> MerchantsByCampaign,
    IReadOnlyDictionary<int, List<string>> CodesByCampaign,
    IReadOnlyDictionary<int, List<string>> ConditionsByCampaign);
