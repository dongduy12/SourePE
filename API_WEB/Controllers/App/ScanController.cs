using API_WEB.ModelsDB;
using Microsoft.AspNetCore.Mvc;

namespace API_WEB.Controllers.App
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScanController : ControllerBase
    {
        private readonly CSDL_NE _context;

        public ScanController(CSDL_NE context)
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

            var log = new ScanLog
            {
                SerialNumber = request.SerialNumber,
                CreatedAt = DateTime.Now
            };

            _context.ScanLogs.Add(log);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Serial number saved successfully." });
        }
    }
    public class SerialNumberRequest
    {
        public string SerialNumber { get; set; } = string.Empty;
    }
}