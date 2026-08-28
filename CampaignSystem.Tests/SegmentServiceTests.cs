using CampaignSystem.Data;
using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Repositories;
using CampaignSystem.Services;
using CampaignSystem.Services.Caching;
using CampaignSystem.Tests.Infrastructure;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignSystem.Tests;

/// <summary>
/// The segment lookup follows the same cache contract as the product one: reads are served
/// from memory, and every write evicts the entry. These two tests pin that the key is wired
/// correctly for segments — a read does not touch the database, and a create is reflected on
/// the next read.
/// </summary>
public class SegmentServiceTests(TestDatabaseFixture fixture) : IClassFixture<TestDatabaseFixture>
{
    private static int _sequence;

    private static SegmentService CreateService(CampaignDbContext context)
        => new(new Repository<Segment>(context),
               new Repository<Customer>(context),
               new Repository<CampaignSegment>(context),
               new LookupCache(new MemoryCache(new MemoryCacheOptions())));

    [Fact]
    public async Task GetAll_ServesFromCache_WithoutReadingTheDatabaseAgain()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var service = CreateService(context);

        var before = await service.GetAllAsync();

        // Inserted behind the service, so the cache is not evicted; a cached read must not see it.
        context.Segments.Add(new Segment { SegmentCode = "S-DIR", SegmentName = "Inserted Behind The Cache" });
        await context.SaveChangesAsync();

        var after = await service.GetAllAsync();

        Assert.Equal(before.Count, after.Count);
        Assert.DoesNotContain(after, s => s.SegmentCode == "S-DIR");
    }

    [Fact]
    public async Task Create_EvictsTheCache_SoTheNextReadIsFresh()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var service = CreateService(context);

        var suffix = Interlocked.Increment(ref _sequence);
        var dto = new CreateSegmentDto { SegmentCode = $"S{suffix}", SegmentName = $"Test Segment {suffix}" };

        await service.GetAllAsync();

        var created = await service.CreateAsync(dto);
        Assert.Equal(ResultStatus.Success, created.Status);

        var after = await service.GetAllAsync();
        Assert.Contains(after, s => s.SegmentCode == dto.SegmentCode);
    }
}
