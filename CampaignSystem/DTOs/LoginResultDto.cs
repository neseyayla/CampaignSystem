namespace CampaignSystem.DTOs;

/// <summary>
/// What a successful sign-in returns.
///
/// The customer id is not in here. It is inside the token, where the customer cannot edit
/// it; handing it back separately would only invite a caller to send it as a parameter.
/// </summary>
public class LoginResultDto
{
    public string Token { get; set; } = null!;

    /// <summary>When the token stops being accepted, so the screen can sign out ahead of it.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Shown in the header. Not proof of anything — the token is.</summary>
    public string CustomerNumber { get; set; } = null!;
}
