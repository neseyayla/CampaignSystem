using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignSystem.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("PRODUCT");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductCode)
            .HasMaxLength(10)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.ProductName)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.ProductCode).IsUnique();

        builder.HasData(
            new Product { Id = 1, ProductCode = "201", ProductName = "Visa Classic" },
            new Product { Id = 2, ProductCode = "202", ProductName = "MasterCard Classic" },
            new Product { Id = 3, ProductCode = "203", ProductName = "Visa Gold" },
            new Product { Id = 4, ProductCode = "204", ProductName = "MasterCard Gold" },
            new Product { Id = 5, ProductCode = "205", ProductName = "Platinum Plus" },
            new Product { Id = 6, ProductCode = "206", ProductName = "Platinum Plus Metal" });
    }
}
