namespace CampaignSystem.Configuration;

/// <summary>
/// Settings for the hosted service that runs the end-of-day job, bound from the
/// "DailyBatch" section.
/// </summary>
public class DailyBatchOptions
{
    public const string SectionName = "DailyBatch";

    /// <summary>
    /// Whether the application runs the job itself. Turn it off when an external scheduler
    /// calls POST /api/batch/run instead — otherwise both would fire, and although a second
    /// run is harmless it makes the logs hard to read.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Local time of day the job runs. Overnight by default: the job reads the whole
    /// transaction table, so it belongs outside business hours.
    /// </summary>
    public TimeOnly RunAt { get; set; } = new(2, 0);
}
