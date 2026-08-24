using CampaignSystem.DTOs;

namespace CampaignSystem.Services;

public interface ISegmentService
{
    Task<List<SegmentDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<SegmentDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ServiceResult<SegmentDto>> CreateAsync(
        CreateSegmentDto dto,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(
        int id,
        UpdateSegmentDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>Hard delete — Segment carries no IsActive flag.</summary>
    Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
