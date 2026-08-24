namespace CampaignSystem.Services;

public enum SetCriteriaStatus
{
    Success,

    /// <summary>No active campaign carries that id.</summary>
    CampaignNotFound,

    /// <summary>The request named a segment, product, merchant or transaction code that does not exist.</summary>
    InvalidReference
}

/// <summary>
/// Why a criteria update succeeded or failed, so the controller can pick the right status
/// code without inspecting exceptions.
/// </summary>
public record SetCriteriaOutcome(SetCriteriaStatus Status, string? Error = null)
{
    public static SetCriteriaOutcome Success() => new(SetCriteriaStatus.Success);

    public static SetCriteriaOutcome CampaignNotFound() => new(SetCriteriaStatus.CampaignNotFound);

    public static SetCriteriaOutcome InvalidReference(string error) =>
        new(SetCriteriaStatus.InvalidReference, error);
}
