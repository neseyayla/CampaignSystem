namespace CampaignSystem.Enums;

/// <summary>
/// Whether the card belongs to the account holder or was issued under their account.
/// Persisted as the string codes shown below.
/// </summary>
public enum CardType
{
    /// <summary>A — primary card, belongs to the account holder.</summary>
    Primary = 1,

    /// <summary>E — supplementary card issued under the same account.</summary>
    Supplementary = 2
}
