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
/// The transaction-code lookup follows the same cache contract as the product one: reads are
/// served from memory, and every write evicts the entry. These two tests pin that the key is
/// wired correctly for transaction codes.
/// </summary>
public class TransactionCodeServiceTests(TestDatabaseFixture fixture) : IClassFixture<TestDatabaseFixture>
{
    private static int _sequence;

    private static TransactionCodeService CreateService(CampaignDbContext context)
        => new(new Repository<TransactionCode>(context),
               new Repository<Transaction>(context),
               new Repository<CampaignTransactionCode>(context),
               new LookupCache(new MemoryCache(new MemoryCacheOptions())));

    [Fact]
    public async Task GetAll_ServesFromCache_WithoutReadingTheDatabaseAgain()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var service = CreateService(context);

        var before = await service.GetAllAsync();

        // Inserted behind the service, so the cache is not evicted; a cached read must not see it.
        context.TransactionCodes.Add(new TransactionCode { Code = "TC-DIR", Name = "Inserted Behind The Cache" });
        await context.SaveChangesAsync();

        var after = await service.GetAllAsync();

        Assert.Equal(before.Count, after.Count);
        Assert.DoesNotContain(after, tc => tc.Code == "TC-DIR");
    }

    [Fact]
    public async Task Create_EvictsTheCache_SoTheNextReadIsFresh()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var service = CreateService(context);

        var suffix = Interlocked.Increment(ref _sequence);
        var dto = new CreateTransactionCodeDto { Code = $"X{suffix}", Name = $"Test Code {suffix}" };

        await service.GetAllAsync();

        var created = await service.CreateAsync(dto);
        Assert.Equal(ResultStatus.Success, created.Status);

        var after = await service.GetAllAsync();
        Assert.Contains(after, tc => tc.Code == dto.Code);
    }
}
