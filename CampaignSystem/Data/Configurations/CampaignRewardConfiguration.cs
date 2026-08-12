using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignSystem.Data.Configurations;

public class CampaignRewardConfiguration : IEntityTypeConfiguration<CampaignReward>
{
    public void Configure(EntityTypeBuilder<CampaignReward> builder)
    {
        builder.ToTable("CAMPAIGN_REWARD");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RewardPoint)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.RewardDate).HasColumnType("datetime2");

        // Guards against a double reward if the batch job is run twice for the same
        // campaign. HasFilter(null) is required: EF Core otherwise adds
        // "WHERE [CardId] IS NOT NULL" to any unique index over a nullable column, which
        // would leave customer level rewards — the ones that carry a null CardId — with no
        // protection at all. Without the filter SQL Server compares two NULLs as equal, so
        // a customer can hold only one customer level reward per campaign.
        builder.HasIndex(x => new { x.CampaignId, x.CustomerId, x.CardId })
            .IsUnique()
            .HasFilter(null);

        builder.HasOne(x => x.Campaign)
            .WithMany(x => x.Rewards)
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.Rewards)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Card)
            .WithMany(x => x.Rewards)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
