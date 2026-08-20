using System.Security.Claims;

namespace CampaignSystem.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The signed-in customer's id, taken from the token.
    ///
    /// Throws rather than returning null: every caller sits behind [Authorize], so a request
    /// that got this far without a usable id means the token was issued wrongly, and quietly
    /// carrying on would mean serving somebody an answer about nobody.
    /// </summary>
    public static int CustomerId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("The token carries no usable customer id.");
    }
}
