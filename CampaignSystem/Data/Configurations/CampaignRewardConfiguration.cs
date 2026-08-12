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
        // campaign. For a customer level reward CardId is null, and the single-NULL
        // behaviour of SQL Server unique indexes limits that to one row per customer.
        builder.HasIndex(x => new { x.CampaignId, x.CustomerId, x.CardId }).IsUnique();

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
