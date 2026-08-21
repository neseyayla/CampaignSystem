namespace CampaignSystem.DTOs;

/// <summary>
/// One campaign together with the customer's standing in it.
///
/// The two are held side by side rather than flattened into one object: the campaign's
/// terms are fixed and the same for everyone, the progress is this customer's alone and
/// changes with every transaction they make.
/// </summary>
public class CustomerCampaignDetailDto
{
    public CustomerCampaignDto Campaign { get; set; } = null!;

    /// <summary>
    /// What the customer would be paid if the campaign were settled at this moment,
    /// produced by the same query the batch will run at the end, so the figure they watch
    /// and the figure they are given cannot drift apart.
    ///
    /// A campaign that has not started yet reports zero rather than nothing: none of its
    /// days have happened, so there is genuinely nothing counted.
    /// </summary>
    public RewardPreviewDto Progress { get; set; } = null!;
}
