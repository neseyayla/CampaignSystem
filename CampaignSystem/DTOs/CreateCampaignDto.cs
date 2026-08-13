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

        if (MinimumAmount is not null && MaximumAmount is not null && MinimumAmount > MaximumAmount)
        {
            yield return new ValidationResult(
                "MinimumAmount cannot be greater than MaximumAmount.",
                [nameof(MinimumAmount)]);
        }
    }
}
