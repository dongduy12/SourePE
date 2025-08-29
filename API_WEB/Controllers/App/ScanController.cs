using API_WEB.Models.SmartFA;
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
        [HttpPost("scanSN")]
        public async Task<IActionResult> SaveSerialNumber([FromBody] SerialNumberScanRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.SerialNumber))
            {
                return BadRequest(new { message = "SerialNumber is required." });
            }

            string sn = request.SerialNumber.Trim().ToUpper();

            // Tìm SN trong DB
            var existingLog = await _context.ScanLogs.FindAsync(sn);

            if (existingLog != null)
            {
                // Nếu đã có thì update thời gian
                existingLog.CreatedAt = DateTime.Now;
                _context.ScanLogs.Update(existingLog);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Serial number updated successfully." });
            }
            else
            {
                // Nếu chưa có thì insert mới
                var log = new ScanLog
                {
                    SerialNumber = sn,
                    CreatedAt = DateTime.Now
                };
                _context.ScanLogs.Add(log);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Serial number saved successfully." });
            }
        }
    }
    public class SerialNumberScanRequest
    {
        public string SerialNumber { get; set; } = string.Empty;
    }
}