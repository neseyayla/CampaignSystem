using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignSystem.Data.Configurations;

public class CampaignClawbackExemptProductConfiguration : IEntityTypeConfiguration<CampaignClawbackExemptProduct>
{
    public void Configure(EntityTypeBuilder<CampaignClawbackExemptProduct> builder)
    {
        builder.ToTable("CAMPAIGN_CLAWBACK_EXEMPT_PRODUCT");

        builder.HasKey(x => new { x.CampaignId, x.ProductId });

        builder.HasOne(x => x.Campaign)
            .WithMany(x => x.ClawbackExemptProducts)
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.ClawbackExemptCampaigns)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
