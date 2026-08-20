namespace CampaignSystem.Configuration;

/// <summary>
/// Settings for the tokens handed to customers, bound from the "Jwt" section.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// The key every token is signed with. Anyone holding it can mint a token for any
    /// customer, so it belongs in User Secrets and never in a tracked file. The application
    /// refuses to start without it rather than falling back to something predictable.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Who issued the token. Checked on every request.</summary>
    public string Issuer { get; set; } = "CampaignSystem";

    /// <summary>Who the token was issued for. Checked on every request.</summary>
    public string Audience { get; set; } = "CampaignSystem.Customer";

    /// <summary>
    /// How long a token stays valid. Short on purpose: there is no revocation list, so a
    /// stolen token is only stopped by running out.
    /// </summary>
    public int LifetimeMinutes { get; set; } = 60;
}
