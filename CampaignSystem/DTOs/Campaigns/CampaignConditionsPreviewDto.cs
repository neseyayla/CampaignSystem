using CampaignSystem.Enums;

namespace CampaignSystem.DTOs;

/// <summary>
/// What the operator has entered so far on the campaign definition screen, before the
/// campaign exists. Every field is optional — the screen sends this on every change while
/// the form is still being filled in, so a half-finished draft must not fail validation the
/// way <see cref="CreateCampaignDto"/> would.
///
/// Deliberately not the same type as <see cref="CreateCampaignDto"/>: reusing it would pull
/// in its [Required] attributes and date-ordering check, which would reject a request the
/// moment the operator starts filling amounts in before naming the campaign.
/// </summary>
public class CampaignConditionsPreviewDto
{
    public CampaignType CampaignType { get; set; } = CampaignType.Mass;

    public EarningType EarningType { get; set; } = EarningType.CardBased;

    public Gender? Gender { get; set; }

    public CardType? CardType { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public decimal? MinimumAmount { get; set; }

    public decimal? MaximumAmount { get; set; }

    public decimal? RewardPoint { get; set; }

    public decimal? MaxRewardAmount { get; set; }

    public bool UnusedPointsClawbackEnabled { get; set; }

    public int? UnusedPointsClawbackDays { get; set; }

    public CampaignCriteriaDto Criteria { get; set; } = new();
}
