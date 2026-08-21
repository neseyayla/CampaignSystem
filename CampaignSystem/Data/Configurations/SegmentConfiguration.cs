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

        // Reference data. Ids are fixed so that a campaign definition can refer to the
        // same segment on every machine and in every environment.
        builder.HasData(
            new Segment { Id = 1, SegmentCode = "OGR", SegmentName = "Öğrenci" },
            new Segment { Id = 2, SegmentCode = "PER", SegmentName = "Şirket Çalışanı" },
            new Segment { Id = 3, SegmentCode = "CFT", SegmentName = "Çiftçi" },
            new Segment { Id = 4, SegmentCode = "EVH", SegmentName = "Ev Hanımı" },
            new Segment { Id = 5, SegmentCode = "EMK", SegmentName = "Emekli" });
    }
}
