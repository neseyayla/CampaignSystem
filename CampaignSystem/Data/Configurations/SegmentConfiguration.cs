using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignSystem.Data.Configurations;

public class SegmentConfiguration : IEntityTypeConfiguration<Segment>
{
    public void Configure(EntityTypeBuilder<Segment> builder)
    {
        builder.ToTable("SEGMENT");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SegmentCode)
            .HasMaxLength(10)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.SegmentName)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.SegmentCode).IsUnique();
    }
}
