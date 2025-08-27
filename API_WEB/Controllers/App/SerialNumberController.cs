using API_WEB.Dtos.App;
using API_WEB.ModelsDB;
using Microsoft.AspNetCore.Mvc;

namespace API_WEB.Controllers.App
{
    [Route("api/[controller]")]
    [ApiController]
    public class SerialNumberController : ControllerBase
    {
        private readonly CSDL_NE _context;

        public SerialNumberController(CSDL_NE context)
        {
            _context = context;
        }

        [HttpPost("save")]
        public async Task<IActionResult> SaveSerialNumber([FromBody] SerialNumberRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.SerialNumber))
            {
                return BadRequest("SerialNumber is required.");
            }

            var log = new SerialNumberLog
            {
                SerialNumber = request.SerialNumber,
                CreatedAt = DateTime.UtcNow
            };

            _context.SerialNumberLogs.Add(log);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Serial number saved successfully." });
        }
    }
}
