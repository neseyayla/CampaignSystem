using CampaignSystem.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CampaignSystem.Data.Converters;

/// <summary>
/// Persists enums as the short codes the bank's existing systems already use, instead of
/// the integer values C# assigns. A row read straight from the database is then readable
/// without a lookup table, and the stored value survives a reordering of the enum members.
/// </summary>
public static class EnumCodeConverters
{
    /// <summary>E = male, K = female.</summary>
    public static readonly ValueConverter<Gender, string> GenderToCode = new(
        value => value == Gender.Male ? "E" : "K",
        code => code == "E" ? Gender.Male : Gender.Female);

    /// <summary>A = primary card, E = supplementary card.</summary>
    public static readonly ValueConverter<CardType, string> CardTypeToCode = new(
        value => value == CardType.Primary ? "A" : "E",
        code => code == "A" ? CardType.Primary : CardType.Supplementary);

    /// <summary>MASS = no enrollment needed, SI = enrollment required.</summary>
    public static readonly ValueConverter<CampaignType, string> CampaignTypeToCode = new(
        value => value == CampaignType.Mass ? "MASS" : "SI",
        code => code == "MASS" ? CampaignType.Mass : CampaignType.EnrollmentRequired);

    /// <summary>K = accumulate per card, M = accumulate per customer.</summary>
    public static readonly ValueConverter<EarningType, string> EarningTypeToCode = new(
        value => value == EarningType.CardBased ? "K" : "M",
        code => code == "K" ? EarningType.CardBased : EarningType.CustomerBased);
}
