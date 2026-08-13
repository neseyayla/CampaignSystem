using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CampaignSystem.Entities;
using CampaignSystem.Dtos;
using CampaignSystem.Repositories;

namespace CampaignSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SegmentsController : ControllerBase
    {
        private readonly IRepository<Segment> _repository;
        public SegmentsController(IRepository<Segment> repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetSegments()
        {
            var segments = await _repository.GetAllAsync();

            var result = segments.Select(s => new
            {
                s.Id,
                s.SegmentCode,
                s.SegmentName,
            });

            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<object>> PostSegments(CreateSegmentRequest request)
        {
            var segment = new Segment
            {
                SegmentCode = request.SegmentCode,
                SegmentName = request.SegmentName
            };

            try
            {
                await _repository.AddAsync(segment);
                await _repository.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict($"'{request.SegmentCode}' kodlu bir segment zaten mevcut. Lütfen farklı bir kod kullanın.");
            }

            return Ok(new
            {
                segment.Id,
                segment.SegmentCode,
                segment.SegmentName
            });
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<object>> PutSegments(int id, CreateSegmentRequest request)
        {
            var segment = await _repository.GetByIdAsync(id);
            if (segment is null)
            {
                return NotFound();
            }

            segment.SegmentCode = request.SegmentCode;
            segment.SegmentName = request.SegmentName;

            try
            {
                await _repository.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict($"'{request.SegmentCode}' kodu başka bir segment tarafından kullanılıyor. Lütfen farklı bir kod kullanın.");
            }

            return Ok(new
            {
                segment.Id,
                segment.SegmentCode,
                segment.SegmentName
            });
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<object>> DeleteSegments(int id)
        {
            var segment = await _repository.GetByIdAsync(id);
            if (segment is null)
            {
                return NotFound();
            }

            try
            {
                _repository.Remove(segment);
                await _repository.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateException)
            {
                return Conflict("Bu istek veri tabanı kuralları gereği gerçekleştirelimiyor (kullanımda olan segment)");
            }
        }
    }
}
