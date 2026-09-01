using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignSystem.Data.Configurations;

public class SeasonalPatternConfiguration : IEntityTypeConfiguration<SeasonalPattern>
{
    public void Configure(EntityTypeBuilder<SeasonalPattern> builder)
    {
        builder.ToTable("SEASONAL_PATTERN");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Month).IsRequired();

        builder.Property(x => x.Weight)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        // One weight per category per month.
        builder.HasIndex(x => new { x.MerchantCategoryId, x.Month }).IsUnique();

        builder.HasOne(x => x.MerchantCategory)
            .WithMany(c => c.SeasonalPatterns)
            .HasForeignKey(x => x.MerchantCategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(BuildSeed());
    }

    /// <summary>
    /// Calendar priors for the categories with a pronounced season. Grounded in ordinary
    /// Turkish retail seasonality — back-to-school in August and September, fuel and travel
    /// across the summer, electronics in November, apparel at the collection changes,
    /// weddings in late spring — rather than measured from this system's own transactions
    /// yet. A category or a month with no entry is an ordinary 1.00. Ids are assigned in a
    /// fixed order so the migration reruns to the same rows.
    /// </summary>
    private static IEnumerable<SeasonalPattern> BuildSeed()
    {
        // MerchantCategory ids, from MerchantCategoryConfiguration:
        //   3 Akaryakıt · 4 Giyim · 5 Ayakkabı & Aksesuar · 7 Elektronik ·
        //   9 Mobilya & Ev Tekstili · 10 Beyaz Eşya · 13 Turizm / Seyahat / Otel ·
        //   14 Havayolları / Ulaşım · 15 Eğitim · 18 Spor · 19 Kuyumculuk / Saat ·
        //   20 Kırtasiye / Oyuncak · 21 Yapı & İnşaat
        (int CategoryId, (int Month, double Weight)[] Months)[] byCategory =
        [
            (3, [(1, 0.85), (2, 0.85), (6, 1.20), (7, 1.35), (8, 1.30), (9, 1.05), (12, 0.95)]),
            (4, [(1, 1.10), (2, 0.90), (3, 1.20), (4, 1.15), (7, 0.85), (9, 1.25), (10, 1.15), (11, 1.20), (12, 1.15)]),
            (5, [(3, 1.15), (4, 1.10), (7, 0.85), (9, 1.25), (10, 1.10), (11, 1.15), (12, 1.10)]),
            (7, [(1, 0.80), (2, 0.80), (3, 0.90), (8, 1.15), (9, 1.20), (11, 1.55), (12, 1.25)]),
            (9, [(1, 0.85), (2, 0.85), (5, 1.25), (6, 1.30), (7, 1.20), (9, 1.10), (11, 1.15)]),
            (10, [(1, 0.80), (2, 0.85), (5, 1.25), (6, 1.30), (7, 1.15), (11, 1.20), (12, 1.15)]),
            (13, [(1, 0.85), (2, 1.15), (3, 0.90), (6, 1.35), (7, 1.55), (8, 1.50), (9, 1.15), (11, 0.80), (12, 0.90)]),
            (14, [(1, 0.90), (2, 1.15), (6, 1.25), (7, 1.45), (8, 1.40), (11, 0.85), (12, 1.10)]),
            (15, [(1, 1.20), (2, 1.25), (4, 0.85), (5, 0.85), (6, 1.10), (7, 1.10), (8, 1.45), (9, 1.60), (10, 1.10), (11, 0.90), (12, 0.85)]),
            (18, [(1, 1.45), (2, 1.15), (6, 0.85), (7, 0.80), (8, 0.85), (9, 1.25), (10, 1.10)]),
            (19, [(2, 1.10), (4, 1.15), (5, 1.35), (6, 1.30), (7, 1.15), (8, 0.85), (9, 0.90), (11, 1.20), (12, 1.25)]),
            (20, [(1, 1.25), (2, 1.20), (4, 1.10), (5, 0.85), (6, 0.80), (7, 0.90), (8, 1.55), (9, 1.60), (10, 0.90), (12, 1.30)]),
            (21, [(1, 0.75), (2, 0.80), (4, 1.20), (5, 1.30), (6, 1.35), (7, 1.30), (8, 1.25), (9, 1.15), (12, 0.80)]),
        ];

        var id = 1;

        foreach (var (categoryId, months) in byCategory)
        {
            foreach (var (month, weight) in months)
            {
                yield return new SeasonalPattern
                {
                    Id = id++,
                    MerchantCategoryId = categoryId,
                    Month = month,
                    Weight = (decimal)weight
                };
            }
        }
    }
}
