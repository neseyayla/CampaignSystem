using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignSystem.Data.Configurations;

public class CampaignSegmentConfiguration : IEntityTypeConfiguration<CampaignSegment>
{
    public void Configure(EntityTypeBuilder<CampaignSegment> builder)
    {
        builder.ToTable("CAMPAIGN_SEGMENT");

        // Composite primary key — the pair itself identifies the row, so the same
        // segment can never be attached to the same campaign twice.
        builder.HasKey(x => new { x.CampaignId, x.SegmentId });

        builder.HasOne(x => x.Campaign)
            .WithMany(x => x.CampaignSegments)
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Segment)
            .WithMany(x => x.CampaignSegments)
            .HasForeignKey(x => x.SegmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
