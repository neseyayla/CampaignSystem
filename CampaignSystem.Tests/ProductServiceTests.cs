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
/// The product lookup is cached because it is read on nearly every campaign screen and
/// changes only through the writes here. These tests pin the two halves of that contract:
/// a read is served from memory rather than the database, and every write evicts the entry
/// so the next read is fresh. Getting either half wrong is invisible on screen until an
/// operator's edit fails to show, or a deleted product lingers in a dropdown.
///
/// Each test runs inside a transaction that is rolled back, and each builds its own cache,
/// so the tests share no state and their order does not matter.
/// </summary>
public class ProductServiceTests(TestDatabaseFixture fixture) : IClassFixture<TestDatabaseFixture>
{
    private static int _sequence;

    private static (ProductService Service, CampaignDbContext Context) CreateService(
        CampaignDbContext context)
        => (new ProductService(
                new Repository<Product>(context),
                new Repository<Card>(context),
                new Repository<CampaignProduct>(context),
                new LookupCache(new MemoryCache(new MemoryCacheOptions()))),
            context);

    private static CreateProductDto NewProductDto()
    {
        var suffix = Interlocked.Increment(ref _sequence);
        return new CreateProductDto { ProductCode = $"T{suffix}", ProductName = $"Test Product {suffix}" };
    }

    [Fact]
    public async Task GetAll_ServesFromCache_WithoutReadingTheDatabaseAgain()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var (service, _) = CreateService(context);

        // First read fills the cache.
        var before = await service.GetAllAsync();

        // A row inserted straight through the context bypasses the service, so it does not
        // evict the cache. If GetAll were reading the database it would return this row; a
        // cached read must not.
        context.Products.Add(new Product { ProductCode = "T-DIRECT", ProductName = "Inserted Behind The Cache" });
        await context.SaveChangesAsync();

        var after = await service.GetAllAsync();

        Assert.Equal(before.Count, after.Count);
        Assert.DoesNotContain(after, p => p.ProductCode == "T-DIRECT");
    }

    [Fact]
    public async Task Create_EvictsTheCache_SoTheNextReadIsFresh()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var (service, _) = CreateService(context);

        var dto = NewProductDto();

        // Prime the cache with a list that does not yet hold the new product.
        var before = await service.GetAllAsync();
        Assert.DoesNotContain(before, p => p.ProductCode == dto.ProductCode);

        var created = await service.CreateAsync(dto);
        Assert.Equal(ResultStatus.Success, created.Status);

        var after = await service.GetAllAsync();
        Assert.Contains(after, p => p.ProductCode == dto.ProductCode);
    }

    [Fact]
    public async Task Update_EvictsTheCache_SoTheNextReadShowsTheNewName()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var (service, _) = CreateService(context);

        var created = (await service.CreateAsync(NewProductDto())).Value!;

        // Read once so the cache holds the pre-edit name.
        await service.GetAllAsync();

        var renamed = "Renamed After Cache";
        var update = await service.UpdateAsync(
            created.Id, new UpdateProductDto { ProductCode = created.ProductCode, ProductName = renamed });
        Assert.Equal(ResultStatus.Success, update.Status);

        var after = await service.GetAllAsync();
        Assert.Contains(after, p => p.Id == created.Id && p.ProductName == renamed);
    }

    [Fact]
    public async Task Delete_EvictsTheCache_SoTheNextReadDropsTheRow()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var (service, _) = CreateService(context);

        var created = (await service.CreateAsync(NewProductDto())).Value!;

        // Read once so the cache holds the row that is about to be deleted.
        var before = await service.GetAllAsync();
        Assert.Contains(before, p => p.Id == created.Id);

        var deleted = await service.DeleteAsync(created.Id);
        Assert.Equal(ResultStatus.Success, deleted.Status);

        var after = await service.GetAllAsync();
        Assert.DoesNotContain(after, p => p.Id == created.Id);
    }
}
