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

    [Required]
    public EarningType EarningType { get; set; }

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

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDate <= StartDate)
        {
            yield return new ValidationResult(
                "EndDate must be later than StartDate.",
                [nameof(EndDate)]);
        }

        if (MinimumAmount is not null && MaximumAmount is not null && MinimumAmount > MaximumAmount)
        {
            yield return new ValidationResult(
                "MinimumAmount cannot be greater than MaximumAmount.",
                [nameof(MinimumAmount)]);
        }
    }
}
