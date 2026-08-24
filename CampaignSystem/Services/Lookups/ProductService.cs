using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Repositories;

namespace CampaignSystem.Services;

/// <summary>
/// Product business rules. No DbContext needed — every check is a single-table question,
/// which the repository already answers.
/// </summary>
public class ProductService(
    IRepository<Product> products,
    IRepository<Card> cards,
    IRepository<CampaignProduct> campaignProducts) : IProductService
{
    public async Task<List<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await products.GetAllAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ProductDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await products.GetByIdAsync(id);
        return product is null ? null : ToDto(product);
    }

    public async Task<ServiceResult<ProductDto>> CreateAsync(
        CreateProductDto dto,
        CancellationToken cancellationToken = default)
    {
        // The database enforces this too, but catching it here produces a readable message
        // instead of a unique index violation.
        if (await products.ExistsAsync(p => p.ProductCode == dto.ProductCode, cancellationToken))
        {
            return ServiceResult<ProductDto>.Conflict(
                $"'{dto.ProductCode}' kodlu bir ürün zaten mevcut. Lütfen farklı bir kod kullanın.");
        }

        var product = new Product
        {
            ProductCode = dto.ProductCode,
            ProductName = dto.ProductName
        };

        await products.AddAsync(product, cancellationToken);
        await products.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProductDto>.Success(ToDto(product));
    }

    public async Task<ServiceResult> UpdateAsync(
        int id,
        UpdateProductDto dto,
        CancellationToken cancellationToken = default)
    {
        var product = await products.GetByIdAsync(id);

        if (product is null)
        {
            return ServiceResult.NotFound();
        }

        if (await products.ExistsAsync(
                p => p.Id != id && p.ProductCode == dto.ProductCode, cancellationToken))
        {
            return ServiceResult.Conflict(
                $"'{dto.ProductCode}' kodu başka bir ürün tarafından kullanılıyor. Lütfen farklı bir kod kullanın.");
        }

        product.ProductCode = dto.ProductCode;
        product.ProductName = dto.ProductName;

        products.Update(product);
        await products.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await products.GetByIdAsync(id);

        if (product is null)
        {
            return ServiceResult.NotFound();
        }

        // The database would reject this too (DeleteBehavior.Restrict on both relations),
        // but checking here produces a readable message instead of a raw FK violation.
        if (await cards.ExistsAsync(c => c.ProductId == id, cancellationToken) ||
            await campaignProducts.ExistsAsync(cp => cp.ProductId == id, cancellationToken))
        {
            return ServiceResult.Conflict(
                "Bu ürün kullanımda olduğu için silinemiyor (bağlı kart veya kampanya var).");
        }

        products.Remove(product);
        await products.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    private static ProductDto ToDto(Product product) => new()
    {
        Id = product.Id,
        ProductCode = product.ProductCode,
        ProductName = product.ProductName
    };
}
