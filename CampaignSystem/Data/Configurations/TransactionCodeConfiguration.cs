using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignSystem.Data.Configurations;

public class TransactionCodeConfiguration : IEntityTypeConfiguration<TransactionCode>
{
    public void Configure(EntityTypeBuilder<TransactionCode> builder)
    {
        builder.ToTable("TRANSACTION_CODE");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .HasMaxLength(10)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasData(
            new TransactionCode { Id = 1, Code = "SA", Name = "Satış" },
            new TransactionCode { Id = 2, Code = "NA", Name = "Nakit Avans" },
            new TransactionCode { Id = 3, Code = "OD", Name = "Borç Ödeme" },
            new TransactionCode { Id = 4, Code = "IA", Name = "İade" },
            // Point redemption: a "PS" transaction records that a customer spent campaign
            // points. The unused-points clawback sums these per card to find what is still
            // sitting unredeemed. See RewardService.ReclaimUnusedPointsAsync.
            new TransactionCode { Id = 5, Code = "PS", Name = "Puan Harcama" });
    }
}
