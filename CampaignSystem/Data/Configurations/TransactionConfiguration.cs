using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignSystem.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("TRANSACTION");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Rrn)
            .HasMaxLength(24)
            .IsUnicode(false);

        builder.Property(x => x.TransactionDate).HasColumnType("datetime2");
        builder.Property(x => x.ClawbackProcessedAt).HasColumnType("datetime2");

        builder.Property(x => x.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        // Rrn is nullable, and SQL Server allows only a single NULL in a plain unique
        // index. The filter excludes the NULL rows so the constraint applies only where
        // a reference number actually exists.
        builder.HasIndex(x => x.Rrn)
            .IsUnique()
            .HasFilter("[Rrn] IS NOT NULL");

        // The two indexes the evaluation batch job scans on.
        builder.HasIndex(x => new { x.CustomerId, x.TransactionDate });
        builder.HasIndex(x => new { x.CardId, x.TransactionDate });

        // A purchase can be refunded more than once (partial refunds), so this is a plain
        // lookup index — not unique — over the refund rows that point back at an original.
        builder.HasIndex(x => x.OriginalTransactionId);

        // The nightly clawback scans only unprocessed refunds; a filtered index keeps that set
        // — normally tiny — cheap to find.
        builder.HasIndex(x => x.ClawbackProcessedAt)
            .HasFilter("[ClawbackProcessedAt] IS NULL AND [OriginalTransactionId] IS NOT NULL");

        builder.HasOne(x => x.Card)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Merchant)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.MerchantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TransactionCode)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.TransactionCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Self-reference: a refund row points at the purchase it reverses.
        builder.HasOne(x => x.OriginalTransaction)
            .WithMany()
            .HasForeignKey(x => x.OriginalTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
