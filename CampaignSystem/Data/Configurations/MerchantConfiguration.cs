using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignSystem.Data.Configurations;

public class MerchantConfiguration : IEntityTypeConfiguration<Merchant>
{
    public void Configure(EntityTypeBuilder<Merchant> builder)
    {
        builder.ToTable("MERCHANT");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MerchantNumber)
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.MerchantName)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(x => x.MerchantNumber).IsUnique();

        // Twelve merchants across the categories a card campaign usually targets: fuel,
        // grocery, restaurant, electronics, clothing. Enough variety that a campaign
        // restricted to one category actually excludes something.
        //
        // Member numbers follow the nine digit form the card scheme issues, rather than the
        // short placeholders this table started with.
        builder.HasData(
            // Restoran
            new Merchant { Id = 1, MerchantNumber = "300145782", MerchantName = "Grande Cafe", IsActive = true },
            new Merchant { Id = 2, MerchantNumber = "300912467", MerchantName = "Köfteci Yusuf", IsActive = true },
            new Merchant { Id = 12, MerchantNumber = "300558214", MerchantName = "Big Chefs", IsActive = true },

            // Akaryakıt
            new Merchant { Id = 3, MerchantNumber = "410874193", MerchantName = "Opet", IsActive = true },
            new Merchant { Id = 4, MerchantNumber = "410336729", MerchantName = "Shell", IsActive = true },
            new Merchant { Id = 5, MerchantNumber = "410771056", MerchantName = "Petrol Ofisi", IsActive = true },

            // Market
            new Merchant { Id = 6, MerchantNumber = "520419863", MerchantName = "Migros", IsActive = true },
            new Merchant { Id = 7, MerchantNumber = "520684137", MerchantName = "BİM", IsActive = true },
            new Merchant { Id = 8, MerchantNumber = "520297540", MerchantName = "A101", IsActive = true },

            // Elektronik
            new Merchant { Id = 9, MerchantNumber = "610853024", MerchantName = "Teknosa", IsActive = true },
            new Merchant { Id = 10, MerchantNumber = "610140678", MerchantName = "Vatan Bilgisayar", IsActive = true },

            // Giyim
            new Merchant { Id = 11, MerchantNumber = "710962385", MerchantName = "LC Waikiki", IsActive = true });
    }
}
