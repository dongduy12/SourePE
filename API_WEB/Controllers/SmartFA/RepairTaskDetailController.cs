using API_WEB.ModelsOracle;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

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

        [HttpPost("data19")]
        public async Task<IActionResult> GetData19BySerials([FromBody] List<string> serialNumbers)
        {
            if (serialNumbers == null || !serialNumbers.Any())
            {
                return BadRequest(new { success = false, message = "Serial numbers are required." });
            }

            var data = await _oracleContext.OracleDataRepairTaskDetail
                .Where(r => serialNumbers.Contains(r.SERIAL_NUMBER) &&
                            (r.DATA17 == "Confirm" || r.DATA17 == "Save" || r.DATA17 == "save" || r.DATA17 == "confirm"))
                .GroupBy(r => r.SERIAL_NUMBER)
                .Select(g => new { SerialNumber = g.Key, Note = string.Join(",", g.Select(r => r.DATA19)) })
                .ToDictionaryAsync(x => x.SerialNumber, x => x.Note);

            return Ok(new { success = true, data });
        }
    }
}
