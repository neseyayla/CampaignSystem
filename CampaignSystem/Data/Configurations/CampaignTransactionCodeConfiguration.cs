using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignSystem.Data.Configurations;

public class CampaignTransactionCodeConfiguration : IEntityTypeConfiguration<CampaignTransactionCode>
{
    public void Configure(EntityTypeBuilder<CampaignTransactionCode> builder)
    {
        builder.ToTable("CAMPAIGN_TRANSACTION_CODE");

        builder.HasKey(x => new { x.CampaignId, x.TransactionCodeId });

        builder.HasOne(x => x.Campaign)
            .WithMany(x => x.CampaignTransactionCodes)
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TransactionCode)
            .WithMany(x => x.CampaignTransactionCodes)
            .HasForeignKey(x => x.TransactionCodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
