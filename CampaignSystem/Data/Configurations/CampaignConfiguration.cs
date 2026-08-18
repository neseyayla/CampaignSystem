using CampaignSystem.Data.Converters;
using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignSystem.Data.Configurations;

public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("CAMPAIGN");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.CampaignType)
            .HasConversion(EnumCodeConverters.CampaignTypeToCode)
            .HasMaxLength(10)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.StartDate).HasColumnType("datetime2");
        builder.Property(x => x.EndDate).HasColumnType("datetime2");

        builder.Property(x => x.MinimumAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.MaximumAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.RewardPoint).HasColumnType("decimal(18,2)");
        builder.Property(x => x.MaxRewardAmount).HasColumnType("decimal(18,2)");

        builder.Property(x => x.EarningType)
            .HasConversion(EnumCodeConverters.EarningTypeToCode)
            .HasMaxLength(2)
            .IsUnicode(false)
            .IsRequired();

        // Optional demographic filters. Null is stored as NULL and means "no restriction",
        // so nothing is written when the campaign does not narrow on that dimension.
        builder.Property(x => x.Gender)
            .HasConversion(EnumCodeConverters.GenderToCode)
            .HasMaxLength(1)
            .IsUnicode(false);

        builder.Property(x => x.CardType)
            .HasConversion(EnumCodeConverters.CardTypeToCode)
            .HasMaxLength(1)
            .IsUnicode(false);

        // Stored as the enum member name; the values are longer than two characters and
        // are only read by this application, so a short code buys nothing here.
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        // Derived from EarningType rather than stored, so there is nothing to map.
        builder.Ignore(x => x.AccumulatesPerCard);

        // The batch job selects campaigns to evaluate by status and end date.
        builder.HasIndex(x => new { x.Status, x.EndDate });
    }
}
