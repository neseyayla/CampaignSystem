using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Repositories;

namespace CampaignSystem.Services;

public class CardService(
    IRepository<Card> cards,
    IRepository<Customer> customers,
    IRepository<Product> products) : ICardService
{
    public async Task<List<CardDto>> GetAllAsync(
        int? customerId = null,
        CancellationToken cancellationToken = default)
    {
        var rows = customerId is null
            ? await cards.FindAsync(c => c.IsActive, cancellationToken)
            : await cards.FindAsync(c => c.IsActive && c.CustomerId == customerId, cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    public async Task<CardDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var card = await cards.GetByIdAsync(id);
        return card is null || !card.IsActive ? null : ToDto(card);
    }

    public async Task<ServiceResult<CardDto>> CreateAsync(
        CreateCardDto dto,
        CancellationToken cancellationToken = default)
    {
        // An inactive customer must not gain new cards, so the check is on IsActive rather
        // than mere existence.
        if (!await customers.ExistsAsync(c => c.Id == dto.CustomerId && c.IsActive, cancellationToken))
        {
            return ServiceResult<CardDto>.Invalid($"Unknown or inactive customer id: {dto.CustomerId}.");
        }

        if (!await products.ExistsAsync(p => p.Id == dto.ProductId, cancellationToken))
        {
            return ServiceResult<CardDto>.Invalid($"Unknown product id: {dto.ProductId}.");
        }

        var card = new Card
        {
            CustomerId = dto.CustomerId,
            ProductId = dto.ProductId,
            CardType = dto.CardType,
            IsActive = true
        };

        await cards.AddAsync(card, cancellationToken);
        await cards.SaveChangesAsync(cancellationToken);

        return ServiceResult<CardDto>.Success(ToDto(card));
    }

    public async Task<ServiceResult> UpdateAsync(
        int id,
        UpdateCardDto dto,
        CancellationToken cancellationToken = default)
    {
        var card = await cards.GetByIdAsync(id);

        if (card is null || !card.IsActive)
        {
            return ServiceResult.NotFound();
        }

        if (!await products.ExistsAsync(p => p.Id == dto.ProductId, cancellationToken))
        {
            return ServiceResult.Invalid($"Unknown product id: {dto.ProductId}.");
        }

        card.ProductId = dto.ProductId;
        card.CardType = dto.CardType;

        cards.Update(card);
        await cards.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var card = await cards.GetByIdAsync(id);

        if (card is null || !card.IsActive)
        {
            return ServiceResult.NotFound();
        }

        // Soft delete. A closed card keeps its transactions and any reward it has earned.
        card.IsActive = false;

        cards.Update(card);
        await cards.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    private static CardDto ToDto(Card card) => new()
    {
        Id = card.Id,
        CustomerId = card.CustomerId,
        ProductId = card.ProductId,
        CardType = card.CardType,
        IsActive = card.IsActive
    };
}
