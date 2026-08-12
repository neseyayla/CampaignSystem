using CampaignSystem.Data.Converters;
using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignSystem.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("CUSTOMER");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerNumber)
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Gender)
            .HasConversion(EnumCodeConverters.GenderToCode)
            .HasMaxLength(1)
            .IsUnicode(false);

        builder.HasIndex(x => x.CustomerNumber).IsUnique();

        builder.HasOne(x => x.Segment)
            .WithMany(x => x.Customers)
            .HasForeignKey(x => x.SegmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
