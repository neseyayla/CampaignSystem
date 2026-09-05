using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignSystem.Data.Configurations;

public class MerchantCategoryConfiguration : IEntityTypeConfiguration<MerchantCategory>
{
    public void Configure(EntityTypeBuilder<MerchantCategory> builder)
    {
        builder.ToTable("MERCHANT_CATEGORY");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CategoryCode)
            .HasMaxLength(10)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.CategoryName)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.CategoryCode).IsUnique();

        // Reference data. Ids are fixed so that a merchant or campaign definition can refer to
        // the same category on every machine and in every environment.
        builder.HasData(
            new MerchantCategory { Id = 1, CategoryCode = "GDA", CategoryName = "Gıda / Market" },
            new MerchantCategory { Id = 2, CategoryCode = "RST", CategoryName = "Restoran / Yeme-İçme" },
            new MerchantCategory { Id = 3, CategoryCode = "AKY", CategoryName = "Akaryakıt" },
            new MerchantCategory { Id = 4, CategoryCode = "GYM", CategoryName = "Giyim" },
            new MerchantCategory { Id = 5, CategoryCode = "AYK", CategoryName = "Ayakkabı & Aksesuar" },
            new MerchantCategory { Id = 6, CategoryCode = "KOZ", CategoryName = "Kozmetik" },
            new MerchantCategory { Id = 7, CategoryCode = "ELK", CategoryName = "Elektronik" },
            new MerchantCategory { Id = 8, CategoryCode = "TEL", CategoryName = "Telekomünikasyon / GSM" },
            new MerchantCategory { Id = 9, CategoryCode = "MOB", CategoryName = "Mobilya & Ev Tekstili" },
            new MerchantCategory { Id = 10, CategoryCode = "BYZ", CategoryName = "Beyaz Eşya" },
            new MerchantCategory { Id = 11, CategoryCode = "OTO", CategoryName = "Otomotiv & Oto Bakım" },
            new MerchantCategory { Id = 12, CategoryCode = "ARK", CategoryName = "Araç Kiralama" },
            new MerchantCategory { Id = 13, CategoryCode = "TUR", CategoryName = "Turizm / Seyahat / Otel" },
            new MerchantCategory { Id = 14, CategoryCode = "HVY", CategoryName = "Havayolları / Ulaşım" },
            new MerchantCategory { Id = 15, CategoryCode = "EGT", CategoryName = "Eğitim" },
            new MerchantCategory { Id = 16, CategoryCode = "SGL", CategoryName = "Sağlık / Eczane / Optik" },
            new MerchantCategory { Id = 17, CategoryCode = "SGR", CategoryName = "Sigorta" },
            new MerchantCategory { Id = 18, CategoryCode = "SPR", CategoryName = "Spor" },
            new MerchantCategory { Id = 19, CategoryCode = "KUY", CategoryName = "Kuyumculuk / Saat" },
            new MerchantCategory { Id = 20, CategoryCode = "KRT", CategoryName = "Kırtasiye / Oyuncak" },
            new MerchantCategory { Id = 21, CategoryCode = "YPI", CategoryName = "Yapı & İnşaat" },
            new MerchantCategory { Id = 22, CategoryCode = "EGL", CategoryName = "Eğlence" });
    }
}
