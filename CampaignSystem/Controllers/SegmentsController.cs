using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CampaignSystem.Data;
using CampaignSystem.Entities;
using CampaignSystem.Dtos;


namespace CampaignSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SegmentsController : ControllerBase
    {
        private readonly CampaignDbContext _context;//readonly sadece constructer içinde tanımlanabilir
        public SegmentsController(CampaignDbContext context) //constructer gelen değeri atıyoruzki işlem yapabilelim
        {
            _context=context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetSegments()
        {
            var segments = await _context.Segments
                .Select(s => new
                {
                    s.Id,
                    s.SegmentCode,
                    s.SegmentName,
                }).ToListAsync();

                return Ok(segments);
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
            _context.Segments.Add(segment);
            await _context.SaveChangesAsync();
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
            var segment = await _context.Segments.FindAsync(id);
            if (segment is null)
            {
                return NotFound();
            }

            segment.SegmentCode = request.SegmentCode;
            segment.SegmentName = request.SegmentName;
            try 
            {
            await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict($"'{request.SegmentCode}' kodu başka bir segment tarafından kullanılıyor. Lütfen farklı bir kod kullanın.");
            }
            return Ok(new {segment.Id,
                      segment.SegmentCode,
                      segment.SegmentName});
                      
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<object>> DeleteSegments(int id)
        {
            var segment = await _context.Segments.FindAsync(id);
            if (segment is null)
            {
                return NotFound();
            }

            try
            {
            _context.Segments.Remove(segment);
            await _context.SaveChangesAsync();
            return NoContent();
            }
            catch(DbUpdateException)
            {
                return Conflict("Bu istek veri tabanı kuralları gereği gerçekleştirelimiyor (kullanımda olan segment)");
            }
            
        }
    }
}