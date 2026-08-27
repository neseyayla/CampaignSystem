using CampaignSystem.Data;
using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Repositories;
using CampaignSystem.Services;
using CampaignSystem.Services.Caching;
using CampaignSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignSystem.Tests;

/// <summary>
/// The merchant lookup follows the same cache contract as the product one, over the active
/// merchants. These two tests pin that the key is wired correctly: a read is served from
/// memory, and an update is reflected on the next read.
///
/// The eviction is exercised through Update rather than Create because a merchant carries a
/// category foreign key that is beside the point of the cache; renaming an existing merchant
/// goes through the same evict path with none of that setup.
/// </summary>
public class MerchantServiceTests(TestDatabaseFixture fixture) : IClassFixture<TestDatabaseFixture>
{
    private static MerchantService CreateService(CampaignDbContext context)
        => new(new Repository<Merchant>(context),
               new LookupCache(new MemoryCache(new MemoryCacheOptions())));

    [Fact]
    public async Task GetAll_ServesFromCache_WithoutReadingTheDatabaseAgain()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var service = CreateService(context);

        var before = await service.GetAllAsync();

        // Reuse an existing merchant's category so the foreign key holds, then insert an active
        // merchant straight through the context — no eviction, so a cached read must not see it.
        var categoryId = await context.Merchants.Select(m => m.MerchantCategoryId).FirstAsync();
        context.Merchants.Add(new Merchant
        {
            MerchantNumber = "M-DIR-0001",
            MerchantName = "Inserted Behind The Cache",
            IsActive = true,
            MerchantCategoryId = categoryId
        });
        await context.SaveChangesAsync();

        var after = await service.GetAllAsync();

        Assert.Equal(before.Count, after.Count);
        Assert.DoesNotContain(after, m => m.MerchantNumber == "M-DIR-0001");
    }

    [Fact]
    public async Task Update_EvictsTheCache_SoTheNextReadShowsTheNewName()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var service = CreateService(context);

        var target = await context.Merchants.FirstAsync(m => m.IsActive);

        // Read once so the cache holds the pre-edit name.
        await service.GetAllAsync();

        var renamed = "Renamed After Cache";
        var update = await service.UpdateAsync(target.Id, new UpdateMerchantDto { MerchantName = renamed });
        Assert.Equal(ResultStatus.Success, update.Status);

        var after = await service.GetAllAsync();
        Assert.Contains(after, m => m.Id == target.Id && m.MerchantName == renamed);
    }
}
