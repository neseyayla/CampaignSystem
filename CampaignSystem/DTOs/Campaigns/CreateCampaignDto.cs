using System.ComponentModel.DataAnnotations;
using CampaignSystem.Enums;

namespace CampaignSystem.DTOs;

/// <summary>
/// What a caller may send when creating a campaign.
///
/// Id, Status and IsActive are absent on purpose: a new campaign always starts as an
/// active draft, and letting the caller set those would let it skip the approval flow.
/// </summary>
public class CreateCampaignDto : IValidatableObject
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public CampaignType CampaignType { get; set; }

    /// <summary>
    /// Required when <see cref="CampaignType"/> is EnrollmentRequired (SI); left null a MASS
    /// campaign has nothing to set it to.
    /// </summary>
    public EnrollmentBasis? EnrollmentBasis { get; set; }

    [Required]
    public EarningType EarningType { get; set; }

    /// <summary>Leave empty to include every gender.</summary>
    public Gender? Gender { get; set; }

    /// <summary>Leave empty to include both primary and supplementary cards.</summary>
    public CardType? CardType { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Range(0, 9999999999999999.99)]
    public decimal? MinimumAmount { get; set; }

    [Range(0, 9999999999999999.99)]
    public decimal? MaximumAmount { get; set; }

    [Range(0, 9999999999999999.99)]
    public decimal? RewardPoint { get; set; }

    [Range(0, 9999999999999999.99)]
    public decimal? MaxRewardAmount { get; set; }

    /// <summary>Whether a refund claws its points back after the campaign has paid.</summary>
    public bool RefundClawbackEnabled { get; set; }

    /// <summary>
    /// Days after the reward is loaded that a refund can still claw points back. Null with
    /// clawback enabled means no time limit.
    /// </summary>
    [Range(0, 3650)]
    public int? RefundClawbackDays { get; set; }

    /// <summary>Whether points never redeemed are clawed back once the window below has passed.</summary>
    public bool UnusedPointsClawbackEnabled { get; set; }

    /// <summary>
    /// Days after the reward is loaded a customer has to redeem it before the unused remainder
    /// is clawed back. Required when unused-points clawback is enabled.
    /// </summary>
    [Range(0, 3650)]
    public int? UnusedPointsClawbackDays { get; set; }

    /// <summary>
    /// Checks that span several properties at once, which a single attribute cannot
    /// express. ASP.NET Core runs this during model binding, so a failure returns 400
    /// before the controller body is reached.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDate <= StartDate)
        {
            yield return new ValidationResult(
                "EndDate must be later than StartDate.",
                [nameof(EndDate)]);
        }

        if (CampaignType == CampaignType.EnrollmentRequired && EnrollmentBasis is null)
        {
            yield return new ValidationResult(
                "EnrollmentBasis is required for an EnrollmentRequired (SI) campaign.",
                [nameof(EnrollmentBasis)]);
        }

        if (MinimumAmount is not null && MaximumAmount is not null && MinimumAmount > MaximumAmount)
        {
            yield return new ValidationResult(
                "MinimumAmount cannot be greater than MaximumAmount.",
                [nameof(MinimumAmount)]);
        }

        if (RefundClawbackEnabled && RefundClawbackDays is null)
        {
            yield return new ValidationResult(
                "RefundClawbackDays is required when refund clawback is enabled.",
                [nameof(RefundClawbackDays)]);
        }

        if (UnusedPointsClawbackEnabled && UnusedPointsClawbackDays is null)
        {
            yield return new ValidationResult(
                "UnusedPointsClawbackDays is required when unused-points clawback is enabled.",
                [nameof(UnusedPointsClawbackDays)]);
        }
    }
}
