using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignSystem.Data.Configurations;

public class CampaignParticipationConfiguration : IEntityTypeConfiguration<CampaignParticipation>
{
    public void Configure(EntityTypeBuilder<CampaignParticipation> builder)
    {
        builder.ToTable("CAMPAIGN_PARTICIPATION");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ParticipationDate).HasColumnType("datetime2");

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        // One enrollment per campaign, customer and card. HasFilter(null) is required:
        // EF Core otherwise adds "WHERE [CardId] IS NOT NULL" to any unique index over a
        // nullable column, which would let a customer enroll at customer level — the case
        // that carries a null CardId — any number of times. Without the filter SQL Server
        // compares two NULLs as equal, so that enrollment can exist only once.
        builder.HasIndex(x => new { x.CampaignId, x.CustomerId, x.CardId })
            .IsUnique()
            .HasFilter(null);

        builder.HasOne(x => x.Campaign)
            .WithMany(x => x.Participations)
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.Participations)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Card)
            .WithMany(x => x.Participations)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
