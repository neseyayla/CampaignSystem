using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CampaignSystem.Data;


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


    }
}