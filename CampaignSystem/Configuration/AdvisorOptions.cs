namespace CampaignSystem.Configuration;

/// <summary>
/// Settings for the AI campaign advisor, bound from the "Advisor" section.
///
/// The advisor is a thin language layer over services that already exist: it never queries
/// the database itself and never computes a figure. It picks which tool to call, reads the
/// numbers those tools return and explains them. Everything it is allowed to say is
/// therefore bounded by <see cref="Services.Advisor.CampaignAdvisorService"/>'s tool list.
/// </summary>
public class AdvisorOptions
{
    public const string SectionName = "Advisor";

    /// <summary>
    /// Anthropic API key. Follows the same rule as the connection string and the JWT signing
    /// key: User Secrets in development, an environment variable elsewhere, an empty
    /// placeholder in appsettings.json. Left empty, the SDK falls back to the
    /// ANTHROPIC_API_KEY environment variable; with neither set the advisor endpoint
    /// refuses rather than the application failing to start, so a developer who does not
    /// need it can still run everything else.
    /// </summary>
    public string ApiKey { get; set; } = "";

    public string Model { get; set; } = "claude-opus-5";

    public int MaxTokens { get; set; } = 16000;

    /// <summary>
    /// How many times the tool loop may go round before it gives up. Each pass is one API
    /// call plus the tools it asks for; the cap stops a model that keeps calling tools
    /// without concluding from running up an unbounded bill.
    /// </summary>
    public int MaxToolIterations { get; set; } = 8;
}
