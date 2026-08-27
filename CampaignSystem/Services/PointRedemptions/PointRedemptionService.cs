using CampaignSystem.Data;
using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampaignSystem.Services;

/// <summary>
/// Records customers redeeming points earned from a campaign. Works against the context
/// directly, the same way <see cref="RewardService"/> does — a redemption touches the
/// campaign, customer and card tables to validate itself, which does not fit behind
/// IRepository.
/// </summary>
public class PointRedemptionService(CampaignDbContext context) : IPointRedemptionService
{
    public async Task<ServiceResult<PointRedemptionDto>> CreateAsync(
        int campaignId,
        CreatePointRedemptionDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!await context.Campaigns.AnyAsync(c => c.Id == campaignId && c.IsActive, cancellationToken))
        {
            return ServiceResult<PointRedemptionDto>.NotFound($"Unknown or inactive campaign id: {campaignId}.");
        }

        if (!await context.Customers.AnyAsync(c => c.Id == dto.CustomerId && c.IsActive, cancellationToken))
        {
            return ServiceResult<PointRedemptionDto>.NotFound($"Unknown or inactive customer id: {dto.CustomerId}.");
        }

        if (dto.CardId is int cardId)
        {
            var card = await context.Cards
                .FirstOrDefaultAsync(c => c.Id == cardId, cancellationToken);

            if (card is null)
            {
                return ServiceResult<PointRedemptionDto>.NotFound($"Unknown card id: {cardId}.");
            }

            if (card.CustomerId != dto.CustomerId)
            {
                return ServiceResult<PointRedemptionDto>.Invalid(
                    $"Card {cardId} does not belong to customer {dto.CustomerId}.");
            }
        }

        var redemption = new PointRedemption
        {
            CampaignId = campaignId,
            CustomerId = dto.CustomerId,
            CardId = dto.CardId,
            Amount = dto.Amount,
            RedemptionDate = dto.RedemptionDate,
            Note = dto.Note
        };

        context.PointRedemptions.Add(redemption);
        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<PointRedemptionDto>.Success(ToDto(redemption));
    }

    public async Task<List<PointRedemptionDto>?> GetByCampaignAsync(
        int campaignId,
        CancellationToken cancellationToken = default)
    {
        if (!await context.Campaigns.AnyAsync(c => c.Id == campaignId && c.IsActive, cancellationToken))
        {
            return null;
        }

        return await context.PointRedemptions
            .AsNoTracking()
            .Where(r => r.CampaignId == campaignId)
            .OrderByDescending(r => r.RedemptionDate)
            .Select(r => new PointRedemptionDto
            {
                Id = r.Id,
                CampaignId = r.CampaignId,
                CustomerId = r.CustomerId,
                CardId = r.CardId,
                Amount = r.Amount,
                RedemptionDate = r.RedemptionDate,
                Note = r.Note
            })
            .ToListAsync(cancellationToken);
    }

    private static PointRedemptionDto ToDto(PointRedemption redemption) => new()
    {
        Id = redemption.Id,
        CampaignId = redemption.CampaignId,
        CustomerId = redemption.CustomerId,
        CardId = redemption.CardId,
        Amount = redemption.Amount,
        RedemptionDate = redemption.RedemptionDate,
        Note = redemption.Note
    };
}
