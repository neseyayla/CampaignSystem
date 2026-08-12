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

        builder.HasData(
            new Merchant { Id = 1, MerchantNumber = "000145", MerchantName = "Grande Cafe", IsActive = true },
            new Merchant { Id = 2, MerchantNumber = "000912", MerchantName = "Köfteci Yusuf", IsActive = true },
            new Merchant { Id = 3, MerchantNumber = "000874", MerchantName = "Opet", IsActive = true });
    }
}
