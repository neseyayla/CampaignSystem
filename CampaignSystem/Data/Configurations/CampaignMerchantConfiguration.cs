using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignSystem.Data.Configurations;

public class CampaignMerchantConfiguration : IEntityTypeConfiguration<CampaignMerchant>
{
    public void Configure(EntityTypeBuilder<CampaignMerchant> builder)
    {
        builder.ToTable("CAMPAIGN_MERCHANT");

        builder.HasKey(x => new { x.CampaignId, x.MerchantId });

        builder.HasOne(x => x.Campaign)
            .WithMany(x => x.CampaignMerchants)
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Merchant)
            .WithMany(x => x.CampaignMerchants)
            .HasForeignKey(x => x.MerchantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
