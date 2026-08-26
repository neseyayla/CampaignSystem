using System.ComponentModel.DataAnnotations;
using CampaignSystem.Enums;

namespace CampaignSystem.DTOs;

/// <summary>
/// What a caller may change on an existing campaign.
///
/// Status is absent here too: moving a campaign from draft to published is an approval
/// step with its own rules, not a field edit. It gets its own endpoint later.
/// </summary>
public class UpdateCampaignDto : IValidatableObject
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

    /// <summary>Days after the reward is loaded that a refund can still claw points back.</summary>
    [Range(0, 3650)]
    public int? RefundClawbackDays { get; set; }

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
    }
}
