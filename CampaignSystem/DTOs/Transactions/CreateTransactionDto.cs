using System.ComponentModel.DataAnnotations;

namespace CampaignSystem.DTOs;

/// <summary>
/// A transaction arriving from the card system.
///
/// CustomerId is not taken from the caller: it follows from the card, and accepting it
/// separately would let the two disagree. The service reads it off the card instead.
/// </summary>
public class CreateTransactionDto
{
    /// <summary>
    /// Retrieval reference number. Optional, but when given it must be unique — this is
    /// what stops the same transaction file being loaded twice and paying the reward twice.
    /// </summary>
    [MaxLength(24)]
    public string? Rrn { get; set; }

    [Required]
    public int CardId { get; set; }

    public int? MerchantId { get; set; }

    [Required]
    public int TransactionCodeId { get; set; }

    [Required]
    public DateTime TransactionDate { get; set; }

    [Range(0.01, 9999999999999999.99)]
    public decimal Amount { get; set; }
}
