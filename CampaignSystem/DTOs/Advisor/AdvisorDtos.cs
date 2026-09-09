using System.ComponentModel.DataAnnotations;

namespace CampaignSystem.DTOs.Advisor;

/// <summary>What the operator typed, in plain language.</summary>
public class AdvisorQuestionDto
{
    [Required]
    [MaxLength(1000)]
    public string Question { get; set; } = null!;
}

/// <summary>
/// The advisor's reply plus the trail it left getting there. <see cref="ToolCalls"/> is not
/// debugging output: it is the evidence behind every figure in <see cref="Answer"/>, and the
/// screen is meant to show it. A campaign that reaches the business unit through this route
/// has to be traceable back to the queries it came from.
/// </summary>
public record AdvisorAnswerDto(
    string Answer,
    IReadOnlyList<AdvisorToolCallDto> ToolCalls);

/// <summary>One tool the model asked for, with the arguments it chose and what came back.</summary>
public record AdvisorToolCallDto(string Tool, string Input, string Output);
