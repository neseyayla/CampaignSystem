using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignSystem.Data.Configurations;

public class PointRedemptionConfiguration : IEntityTypeConfiguration<PointRedemption>
{
    public void Configure(EntityTypeBuilder<PointRedemption> builder)
    {
        builder.ToTable("POINT_REDEMPTION");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.RedemptionDate).HasColumnType("datetime2");

        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasOne(x => x.Campaign)
            .WithMany()
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.PointRedemptions)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Card)
            .WithMany(x => x.PointRedemptions)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Restrict);

        // The clawback sweep sums redemptions per campaign and groups them by customer/card.
        builder.HasIndex(x => new { x.CampaignId, x.CustomerId, x.CardId });
    }
}
