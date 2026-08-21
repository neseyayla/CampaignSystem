using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CampaignSystem.Configuration;
using CampaignSystem.Data;
using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CampaignSystem.Services;

/// <summary>
/// Signs customers in and issues the token the rest of their session runs on.
///
/// The token carries the customer's id as a claim, and every customer facing endpoint reads
/// it from there rather than from the URL. That is the whole point of this class: an id that
/// never travels as a parameter cannot be swapped for somebody else's.
/// </summary>
public class AuthService(
    CampaignDbContext context,
    IOptions<JwtOptions> options) : IAuthService
{
    /// <summary>
    /// The same message for every kind of failure, so the endpoint cannot be used to find
    /// out which customer numbers are real.
    /// </summary>
    private const string RejectionMessage = "Müşteri numarası veya şifre hatalı.";

    /// <summary>
    /// The staff equivalent, worded for the admin sign-in where the field is a personnel id.
    /// Like <see cref="RejectionMessage"/>, one and the same message for every kind of
    /// failure so the endpoint cannot be used to tell which ids exist or carry the flag.
    /// </summary>
    private const string AdminRejectionMessage = "Personel ID veya şifre hatalı.";

    private readonly PasswordHasher<Customer> _hasher = new();

    public async Task<ServiceResult<LoginResultDto>> LoginAsync(
        LoginDto dto,
        CancellationToken cancellationToken = default)
    {
        var customer = await context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerNumber == dto.CustomerNumber, cancellationToken);

        // A closed customer and a customer who was never given a password are both refused
        // here, and refused the same way as a wrong password.
        if (customer is null || !customer.IsActive || string.IsNullOrEmpty(customer.PasswordHash))
        {
            return ServiceResult<LoginResultDto>.Invalid(RejectionMessage);
        }

        var verification = _hasher.VerifyHashedPassword(customer, customer.PasswordHash, dto.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return ServiceResult<LoginResultDto>.Invalid(RejectionMessage);
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(options.Value.LifetimeMinutes);

        return ServiceResult<LoginResultDto>.Success(new LoginResultDto
        {
            Token = CreateToken(customer, "Customer", expiresAt),
            ExpiresAt = expiresAt,
            CustomerNumber = customer.CustomerNumber
        });
    }

    public async Task<ServiceResult<LoginResultDto>> AdminLoginAsync(
        LoginDto dto,
        CancellationToken cancellationToken = default)
    {
        var customer = await context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerNumber == dto.CustomerNumber, cancellationToken);

        // A row that is not an admin is refused exactly like a wrong password, so an ordinary
        // customer cannot tell from the response whether the account exists or simply lacks
        // the admin flag — and can never obtain a staff token.
        if (customer is null || !customer.IsActive || !customer.IsAdmin ||
            string.IsNullOrEmpty(customer.PasswordHash))
        {
            return ServiceResult<LoginResultDto>.Invalid(AdminRejectionMessage);
        }

        var verification = _hasher.VerifyHashedPassword(customer, customer.PasswordHash, dto.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return ServiceResult<LoginResultDto>.Invalid(AdminRejectionMessage);
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(options.Value.LifetimeMinutes);

        return ServiceResult<LoginResultDto>.Success(new LoginResultDto
        {
            Token = CreateToken(customer, "Admin", expiresAt),
            ExpiresAt = expiresAt,
            CustomerNumber = customer.CustomerNumber
        });
    }

    public async Task<ServiceResult> SetPasswordAsync(
        int customerId,
        string password,
        CancellationToken cancellationToken = default)
    {
        var customer = await context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.IsActive, cancellationToken);

        if (customer is null)
        {
            return ServiceResult.NotFound();
        }

        // Only the hash is written. The clear value goes out of scope here and is never
        // logged, never returned and never stored.
        customer.PasswordHash = _hasher.HashPassword(customer, password);

        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ChangePasswordAsync(
        int customerId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var customer = await context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.IsActive, cancellationToken);

        if (customer is null)
        {
            return ServiceResult.NotFound();
        }

        // A customer with no password set cannot verify a current one; that is a branch reset,
        // not a self-service change.
        if (string.IsNullOrEmpty(customer.PasswordHash) ||
            _hasher.VerifyHashedPassword(customer, customer.PasswordHash, currentPassword)
                == PasswordVerificationResult.Failed)
        {
            return ServiceResult.Invalid("Mevcut şifre hatalı.");
        }

        // Only the new hash is written; both clear values go out of scope here.
        customer.PasswordHash = _hasher.HashPassword(customer, newPassword);

        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    public string HashPassword(string password) => _hasher.HashPassword(new Customer(), password);

    private string CreateToken(Customer customer, string role, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.SigningKey));

        var claims = new List<Claim>
        {
            // The id the customer endpoints read. Nothing else identifies the caller.
            new(ClaimTypes.NameIdentifier, customer.Id.ToString()),
            new(ClaimTypes.Name, customer.CustomerNumber),

            // "Customer" or "Admin". Marks which application the token was minted for so the
            // two cannot be used interchangeably, and lets the staff endpoints demand "Admin".
            new(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            issuer: options.Value.Issuer,
            audience: options.Value.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
