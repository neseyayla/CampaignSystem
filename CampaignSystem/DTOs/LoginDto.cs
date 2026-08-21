using System.ComponentModel.DataAnnotations;

namespace CampaignSystem.DTOs;

/// <summary>
/// A customer's sign-in attempt.
///
/// The password arrives in clear and must stay out of every log, every error message and
/// every response. It is hashed the moment it is checked and nothing keeps a copy.
/// </summary>
public class LoginDto
{
    [Required]
    [MaxLength(20)]
    public string CustomerNumber { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public string Password { get; set; } = null!;
}
