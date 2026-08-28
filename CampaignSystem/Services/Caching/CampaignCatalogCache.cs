using Microsoft.Extensions.Caching.Memory;

namespace CampaignSystem.Services.Caching;

/// <summary>
/// Caches the shared <see cref="CampaignCatalog"/> — the six queries the customer campaign
/// list runs identically for everyone. Separate from <see cref="LookupCache"/> on purpose:
/// its policy is different. The TTL is short because the catalog also turns over on its own
/// when the nightly batch moves a campaign out of the open statuses, and eviction is driven
/// from several places — the campaign write endpoints and that batch — so the key and expiry
/// stay hidden behind <see cref="GetOrBuildAsync"/> / <see cref="Invalidate"/> rather than
/// being passed in.
///
/// The TTL is the safety net, not the freshness mechanism: writes evict eagerly, and an
/// eviction a caller forgets costs at most this long, never a stale list forever.
/// </summary>
public class CampaignCatalogCache(IMemoryCache cache)
{
    private const string Key = "campaign:catalog";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    public Task<CampaignCatalog> GetOrBuildAsync(Func<Task<CampaignCatalog>> build)
        => cache.GetOrCreateAsync(Key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return build();
        })!;

    public void Invalidate() => cache.Remove(Key);
}
