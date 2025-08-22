using API_WEB.ModelsOracle;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_WEB.Controllers.SmartFA
{
    [Route("api/[controller]")]
    [ApiController]
    public class RepairTaskDetailController : ControllerBase
    {
        private readonly OracleDbContext _oracleContext;

        public RepairTaskDetailController(OracleDbContext oracleContext)
        {
            _oracleContext = oracleContext;
        }

        [HttpGet("data19/{serialNumber}")]
        public async Task<IActionResult> GetData19BySerial(string serialNumber)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                return BadRequest(new { success = false, message = "Serial number is required." });
            }

            var dataList = await _oracleContext.OracleDataRepairTaskDetail
                .Where(r => r.SERIAL_NUMBER == serialNumber &&
                            (r.DATA17 == "Confirm" || r.DATA17 == "Save" || r.DATA17 == "save" || r.DATA17 == "confirm"))
                .Select(r => r.DATA19)
                .ToListAsync();

            var data = string.Join(",", dataList);

            return Ok(new { success = true, data });
        }
    }
}
