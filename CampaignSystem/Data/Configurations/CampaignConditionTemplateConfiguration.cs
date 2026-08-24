using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignSystem.Data.Configurations;

public class CampaignConditionTemplateConfiguration : IEntityTypeConfiguration<CampaignConditionTemplate>
{
    public void Configure(EntityTypeBuilder<CampaignConditionTemplate> builder)
    {
        builder.ToTable("CAMPAIGN_CONDITION_TEMPLATE");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key)
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.TemplateText)
            .HasMaxLength(500)
            .IsRequired();

        builder.HasIndex(x => x.Key).IsUnique();

        // Reference data, one row per CampaignService.BuildAutoConditionTextsAsync scenario.
        // Ids are fixed so a row can be told apart from its Key across environments; Key is
        // what the code reads, TemplateText is the only column meant to be hand-edited.
        builder.HasData(
            new CampaignConditionTemplate
            {
                Id = 1,
                Key = "DateRange",
                TemplateText = "Kampanya {StartDate} - {EndDate} tarihleri arasında geçerlidir.",
                IsActive = true
            },
            new CampaignConditionTemplate
            {
                Id = 2,
                Key = "EnrollmentRequired",
                TemplateText = "Kampanyadan yararlanabilmek için kampanyaya katılım sağlanması gerekmektedir.",
                IsActive = true
            },
            new CampaignConditionTemplate
            {
                Id = 3,
                Key = "MinAndMaxAmount",
                TemplateText = "İşlem tutarı {MinimumAmount} TL ile {MaximumAmount} TL arasında olmalıdır.",
                IsActive = true
            },
            new CampaignConditionTemplate
            {
                Id = 4,
                Key = "MinAmountOnly",
                TemplateText = "En az {MinimumAmount} TL tutarında işlem yapılması gerekmektedir.",
                IsActive = true
            },
            new CampaignConditionTemplate
            {
                Id = 5,
                Key = "MaxAmountOnly",
                TemplateText = "İşlem tutarı en fazla {MaximumAmount} TL olmalıdır.",
                IsActive = true
            },
            new CampaignConditionTemplate
            {
                Id = 6,
                Key = "RewardPoint",
                TemplateText = "Uygun her işlem için {RewardPoint} TL Worldpuan kazandırır.",
                IsActive = true
            },
            new CampaignConditionTemplate
            {
                Id = 7,
                Key = "MaxRewardAmount",
                TemplateText = "Kampanya kapsamında {PerUnit} başına en fazla {MaxRewardAmount} TL Worldpuan kazanılabilir.",
                IsActive = true
            },
            new CampaignConditionTemplate
            {
                Id = 8,
                Key = "Gender",
                TemplateText = "Kampanya yalnızca {GenderText} müşteriler için geçerlidir.",
                IsActive = true
            },
            new CampaignConditionTemplate
            {
                Id = 9,
                Key = "CardType",
                TemplateText = "Kampanya yalnızca {CardTypeText} kartlar için geçerlidir.",
                IsActive = true
            },
            new CampaignConditionTemplate
            {
                Id = 10,
                Key = "SegmentList",
                TemplateText = "Kampanya yalnızca şu müşteri gruplarına açıktır: {Names}.",
                IsActive = true
            },
            new CampaignConditionTemplate
            {
                Id = 11,
                Key = "ProductList",
                TemplateText = "Kampanya yalnızca şu kart tiplerinde geçerlidir: {Names}.",
                IsActive = true
            },
            new CampaignConditionTemplate
            {
                Id = 12,
                Key = "MerchantList",
                TemplateText = "Kampanya yalnızca şu üye işyerlerinde yapılan alışverişlerde geçerlidir: {Names}.",
                IsActive = true
            },
            new CampaignConditionTemplate
            {
                Id = 13,
                Key = "TransactionCodeList",
                TemplateText = "Kampanya yalnızca şu işlem türlerinde geçerlidir: {Names}.",
                IsActive = true
            });
    }
}
