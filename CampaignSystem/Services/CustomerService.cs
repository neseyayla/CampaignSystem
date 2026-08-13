using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Repositories;

namespace CampaignSystem.Services;

/// <summary>
/// Customer business rules.
///
/// Unlike <see cref="CampaignService"/> this one needs no DbContext: every check it makes
/// is a single-table question, which the repository already answers.
/// </summary>
public class CustomerService(
    IRepository<Customer> customers,
    IRepository<Segment> segments) : ICustomerService
{
    public async Task<List<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await customers.FindAsync(c => c.IsActive, cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<CustomerDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var customer = await customers.GetByIdAsync(id);
        return customer is null || !customer.IsActive ? null : ToDto(customer);
    }

    public async Task<ServiceResult<CustomerDto>> CreateAsync(
        CreateCustomerDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.SegmentId is not null &&
            !await segments.ExistsAsync(s => s.Id == dto.SegmentId, cancellationToken))
        {
            return ServiceResult<CustomerDto>.Invalid($"Unknown segment id: {dto.SegmentId}.");
        }

        // The database enforces this too, but catching it here produces a readable message
        // instead of a unique index violation.
        if (await customers.ExistsAsync(c => c.CustomerNumber == dto.CustomerNumber, cancellationToken))
        {
            return ServiceResult<CustomerDto>.Conflict(
                $"Customer number {dto.CustomerNumber} is already in use.");
        }

        var customer = new Customer
        {
            CustomerNumber = dto.CustomerNumber,
            Gender = dto.Gender,
            SegmentId = dto.SegmentId,
            IsActive = true
        };

        await customers.AddAsync(customer, cancellationToken);
        await customers.SaveChangesAsync(cancellationToken);

        return ServiceResult<CustomerDto>.Success(ToDto(customer));
    }

    public async Task<ServiceResult> UpdateAsync(
        int id,
        UpdateCustomerDto dto,
        CancellationToken cancellationToken = default)
    {
        var customer = await customers.GetByIdAsync(id);

        if (customer is null || !customer.IsActive)
        {
            return ServiceResult.NotFound();
        }

        if (dto.SegmentId is not null &&
            !await segments.ExistsAsync(s => s.Id == dto.SegmentId, cancellationToken))
        {
            return ServiceResult.Invalid($"Unknown segment id: {dto.SegmentId}.");
        }

        customer.Gender = dto.Gender;
        customer.SegmentId = dto.SegmentId;

        customers.Update(customer);
        await customers.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var customer = await customers.GetByIdAsync(id);

        if (customer is null || !customer.IsActive)
        {
            return ServiceResult.NotFound();
        }

        // Soft delete. The customer's cards, transactions and rewards stay intact.
        customer.IsActive = false;

        customers.Update(customer);
        await customers.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    private static CustomerDto ToDto(Customer customer) => new()
    {
        Id = customer.Id,
        CustomerNumber = customer.CustomerNumber,
        Gender = customer.Gender,
        SegmentId = customer.SegmentId,
        IsActive = customer.IsActive
    };
}
