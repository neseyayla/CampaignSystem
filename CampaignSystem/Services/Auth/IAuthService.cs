using CampaignSystem.DTOs;

namespace CampaignSystem.Services;

public interface IAuthService
{
    /// <summary>
    /// Checks a customer's credentials and issues a token.
    ///
    /// Returns Invalid with one and the same message whether the customer number is unknown,
    /// the customer is closed, no password was ever set, or the password is simply wrong.
    /// Telling those apart would turn this into a way of discovering which customer numbers
    /// exist, one guess at a time.
    /// </summary>
    Task<ServiceResult<LoginResultDto>> LoginAsync(
        LoginDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks a staff member's credentials and issues an admin token.
    ///
    /// Identical to <see cref="LoginAsync"/> but refuses any row that is not flagged as an
    /// admin, with the same single message, so a customer can neither obtain a staff token nor
    /// learn from the response whether an account lacks the flag or does not exist at all.
    /// </summary>
    Task<ServiceResult<LoginResultDto>> AdminLoginAsync(
        LoginDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives a customer a password, or replaces the one they had. NotFound when no active
    /// customer carries that id.
    /// </summary>
    Task<ServiceResult> SetPasswordAsync(
        int customerId,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the customer's own password after checking the current one.
    ///
    /// NotFound when no active customer carries that id; Invalid when the current password
    /// does not match. Holding a token is not enough on its own — the current password must
    /// be given too, so an unlocked screen cannot be used to lock the owner out.
    /// </summary>
    Task<ServiceResult> ChangePasswordAsync(
        int customerId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hashes a password for storage. Used when a password is set, never to compare — a
    /// hash is salted per call, so two hashes of the same password do not match.
    /// </summary>
    string HashPassword(string password);
}
