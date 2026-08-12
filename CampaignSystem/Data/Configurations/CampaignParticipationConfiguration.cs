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

        // One enrollment per campaign, customer and card. SQL Server treats two NULLs as
        // equal inside a unique index, which is exactly what is wanted here: a customer
        // can hold only one customer level enrollment (CardId null) per campaign.
        builder.HasIndex(x => new { x.CampaignId, x.CustomerId, x.CardId }).IsUnique();

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
