using Microsoft.Extensions.Caching.Memory;

namespace CampaignSystem.Services.Caching;

/// <summary>
/// Thin wrapper over <see cref="IMemoryCache"/> for the reference-data lookups — products,
/// segments, merchants, transaction codes. Each of those lists is read on nearly every
/// campaign screen and changes only through its own admin writes, so each service caches its
/// list under a key and evicts that key on every write.
///
/// The caching policy lives here rather than being copied into four services: the expiry,
/// and the fact that it is absolute rather than sliding. The TTL is a safety net, not the
/// freshness mechanism — writes evict eagerly, and an eviction the write path forgets costs
/// at most this long, never a stale read forever.
/// </summary>
public class LookupCache(IMemoryCache cache)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    public Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory)
        => cache.GetOrCreateAsync(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return factory();
        })!;

    public void Remove(string key) => cache.Remove(key);
}
