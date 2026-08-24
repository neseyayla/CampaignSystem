using System.ComponentModel.DataAnnotations;

namespace CampaignSystem.DTOs;

/// <summary>
/// The customer changing their own password.
///
/// The current password is required and checked: holding a valid token is not enough to
/// set a new password, so a borrowed unlocked screen cannot be used to lock the real owner
/// out. Both values arrive in clear and are gone the moment the new one is hashed.
/// </summary>
public class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; } = null!;

    [Required]
    [MinLength(6)]
    [MaxLength(128)]
    public string NewPassword { get; set; } = null!;
}
