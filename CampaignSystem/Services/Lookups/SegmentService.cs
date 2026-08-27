using CampaignSystem.DTOs;
using CampaignSystem.Entities;
using CampaignSystem.Repositories;
using CampaignSystem.Services.Caching;

namespace CampaignSystem.Services;

/// <summary>
/// Segment business rules. No DbContext needed — every check is a single-table question,
/// which the repository already answers.
///
/// The segment list is read on every campaign screen and changes only through the writes
/// here, so it is cached through <see cref="LookupCache"/> and each write evicts the key.
/// </summary>
public class SegmentService(
    IRepository<Segment> segments,
    IRepository<Customer> customers,
    IRepository<CampaignSegment> campaignSegments,
    LookupCache cache) : ISegmentService
{
    private const string ListCacheKey = "lookup:segments";

    public Task<List<SegmentDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => cache.GetOrCreateAsync(ListCacheKey, async () =>
        {
            var rows = await segments.GetAllAsync(cancellationToken);
            return rows.Select(ToDto).ToList();
        });

    public async Task<SegmentDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var segment = await segments.GetByIdAsync(id);
        return segment is null ? null : ToDto(segment);
    }

    public async Task<ServiceResult<SegmentDto>> CreateAsync(
        CreateSegmentDto dto,
        CancellationToken cancellationToken = default)
    {
        // The database enforces this too, but catching it here produces a readable message
        // instead of a unique index violation.
        if (await segments.ExistsAsync(s => s.SegmentCode == dto.SegmentCode, cancellationToken))
        {
            return ServiceResult<SegmentDto>.Conflict(
                $"'{dto.SegmentCode}' kodlu bir segment zaten mevcut. Lütfen farklı bir kod kullanın.");
        }

        var segment = new Segment
        {
            SegmentCode = dto.SegmentCode,
            SegmentName = dto.SegmentName
        };

        await segments.AddAsync(segment, cancellationToken);
        await segments.SaveChangesAsync(cancellationToken);
        cache.Remove(ListCacheKey);

        return ServiceResult<SegmentDto>.Success(ToDto(segment));
    }

    public async Task<ServiceResult> UpdateAsync(
        int id,
        UpdateSegmentDto dto,
        CancellationToken cancellationToken = default)
    {
        var segment = await segments.GetByIdAsync(id);

        if (segment is null)
        {
            return ServiceResult.NotFound();
        }

        if (await segments.ExistsAsync(
                s => s.Id != id && s.SegmentCode == dto.SegmentCode, cancellationToken))
        {
            return ServiceResult.Conflict(
                $"'{dto.SegmentCode}' kodu başka bir segment tarafından kullanılıyor. Lütfen farklı bir kod kullanın.");
        }

        segment.SegmentCode = dto.SegmentCode;
        segment.SegmentName = dto.SegmentName;

        segments.Update(segment);
        await segments.SaveChangesAsync(cancellationToken);
        cache.Remove(ListCacheKey);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var segment = await segments.GetByIdAsync(id);

        if (segment is null)
        {
            return ServiceResult.NotFound();
        }

        // The database would reject this too (DeleteBehavior.Restrict on both relations),
        // but checking here produces a readable message instead of a raw FK violation.
        if (await customers.ExistsAsync(c => c.SegmentId == id, cancellationToken) ||
            await campaignSegments.ExistsAsync(cs => cs.SegmentId == id, cancellationToken))
        {
            return ServiceResult.Conflict(
                "Bu segment kullanımda olduğu için silinemiyor (bağlı müşteri veya kampanya var).");
        }

        segments.Remove(segment);
        await segments.SaveChangesAsync(cancellationToken);
        cache.Remove(ListCacheKey);

        return ServiceResult.Success();
    }

    private static SegmentDto ToDto(Segment segment) => new()
    {
        Id = segment.Id,
        SegmentCode = segment.SegmentCode,
        SegmentName = segment.SegmentName
    };
}
