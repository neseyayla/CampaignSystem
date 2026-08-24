using System.ComponentModel.DataAnnotations;

namespace CampaignSystem.DTOs;

/// <summary>
/// Gives a customer a password, or replaces the one they had.
///
/// The value arrives in clear and is hashed before anything is written. Nothing keeps a
/// copy and no endpoint ever reads one back.
/// </summary>
public class SetPasswordDto
{
    [Required]
    [MinLength(6)]
    [MaxLength(128)]
    public string Password { get; set; } = null!;
}
