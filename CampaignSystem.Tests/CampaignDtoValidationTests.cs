using System.ComponentModel.DataAnnotations;
using CampaignSystem.DTOs;
using CampaignSystem.Enums;

namespace CampaignSystem.Tests;

/// <summary>
/// Cross-field rules on the campaign DTOs, checked without hitting the database.
/// </summary>
public class CampaignDtoValidationTests
{
    private static CreateCampaignDto ValidBase() => new()
    {
        Name = "Test",
        CampaignType = CampaignType.Mass,
        EarningType = EarningType.CardBased,
        StartDate = DateTime.Now,
        EndDate = DateTime.Now.AddDays(1)
    };

    [Fact]
    public void ClawbackDays_IsRequired_WhenClawbackIsEnabled()
    {
        var dto = ValidBase();
        dto.RefundClawbackEnabled = true;
        dto.RefundClawbackDays = null;

        var results = dto.Validate(new ValidationContext(dto));

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateCampaignDto.RefundClawbackDays)));
    }

    [Fact]
    public void ClawbackDays_MayBeOmitted_WhenClawbackIsOff()
    {
        var dto = ValidBase();
        dto.RefundClawbackEnabled = false;
        dto.RefundClawbackDays = null;

        var results = dto.Validate(new ValidationContext(dto));

        Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(CreateCampaignDto.RefundClawbackDays)));
    }
}
