using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignSystem.Data.Configurations;

public class CampaignConditionConfiguration : IEntityTypeConfiguration<CampaignCondition>
{
    public void Configure(EntityTypeBuilder<CampaignCondition> builder)
    {
        builder.ToTable("CAMPAIGN_CONDITION");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Text)
            .HasMaxLength(500)
            .IsRequired();

        builder.HasOne(x => x.Campaign)
            .WithMany(x => x.Conditions)
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
