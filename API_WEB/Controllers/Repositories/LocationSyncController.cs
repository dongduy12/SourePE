using API_WEB.ModelsDB;
using API_WEB.ModelsOracle;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Oracle.ManagedDataAccess.Client;
using System.Text;

namespace API_WEB.Controllers.Repositories
{
    [Route("api/[controller]")]
    [ApiController]
    public class LocationSyncController : ControllerBase
    {
        private readonly CSDL_NE _sqlContext;
        private readonly OracleDbContext _oracleContext;
        private readonly IHttpClientFactory _httpClientFactory;
        public LocationSyncController(CSDL_NE sqlContext, OracleDbContext oracleContext, IHttpClientFactory httpClientFactory)
        {
            _sqlContext = sqlContext;
            _oracleContext = oracleContext;
            _httpClientFactory = httpClientFactory;
        }

        public class UpdateLocationRequest
        {
            public List<string> SerialNumbers { get; set; } = new();
        }

        [HttpPost("update-data18")]
        public async Task<IActionResult> UpdateData18([FromBody] UpdateLocationRequest request)
        {
            if (request == null || request.SerialNumbers == null || !request.SerialNumbers.Any())
            {
                return BadRequest(new { success = false, message = "Danh sách Serial Number không hợp lệ." });
            }

            var results = new List<object>();
            foreach (var sn in request.SerialNumbers.Select(sn => sn?.Trim()).Where(sn => !string.IsNullOrEmpty(sn)))
            {
                var info = await GetLocationAndTagAsync(sn!);
                if (info.location == null)
                {
                    results.Add(new { serialNumber = sn, success = false, message = "Không tìm thấy vị trí." });
                    continue;
                }

                await SendReceivingStatusAsync(new[] { sn! }, string.Empty, info.location, info.tag);
                results.Add(new { serialNumber = sn, success = true, location = info.location });
            }

            return Ok(new { success = true, results });
        }

        private async Task<(string? location, string tag)> GetLocationAndTagAsync(string serialNumber)
        {
            var scrap = await _sqlContext.KhoScraps.AsNoTracking().FirstOrDefaultAsync(k => k.SERIAL_NUMBER == serialNumber);
            if (scrap != null && !string.IsNullOrEmpty(scrap.ShelfCode) && scrap.ColumnNumber.HasValue && scrap.LevelNumber.HasValue && scrap.TrayNumber.HasValue)
            {
                var loc = $"{scrap.ShelfCode}{scrap.ColumnNumber}-{scrap.LevelNumber}-K{scrap.TrayNumber}";
                return (loc, "Nhận(Nhập Kho Phế)");
            }

            var ok = await _sqlContext.KhoOks.AsNoTracking().FirstOrDefaultAsync(k => k.SERIAL_NUMBER == serialNumber);
            if (ok != null && !string.IsNullOrEmpty(ok.ShelfCode) && ok.ColumnNumber.HasValue && ok.LevelNumber.HasValue)
            {
                var loc = $"{ok.ShelfCode}{ok.ColumnNumber}-{ok.LevelNumber}";
                return (loc, "Nhận(Nhập Kho Ok)");
            }

            var product = await _sqlContext.Products.AsNoTracking().Include(p => p.Shelf).FirstOrDefaultAsync(p => p.SerialNumber == serialNumber);
            if (product != null && product.Shelf != null && !string.IsNullOrEmpty(product.Shelf.ShelfCode) && product.ColumnNumber.HasValue && product.LevelNumber.HasValue && product.TrayNumber.HasValue)
            {
                var loc = $"{product.Shelf.ShelfCode}{product.ColumnNumber}-{product.LevelNumber}-K{product.TrayNumber}";
                return (loc, "Nhận(Nhập Kho)");
            }

            return (null, string.Empty);
        }

        [HttpPost("sync-repair-task")]
        public async Task<IActionResult> SyncRepairTask()
        {
            var serialNumbers = await _oracleContext.OracleDataRepairTask
                .AsNoTracking()
                .Select(r => r.SERIAL_NUMBER)
                .ToListAsync();

            var results = new List<object>();
            foreach (var sn in serialNumbers)
            {
                var info = await GetLocationAndTagAsync(sn);
                if (info.location == null) continue;

                await SendReceivingStatusAsync(new[] { sn }, string.Empty, info.location, info.tag);
                results.Add(new { serialNumber = sn, location = info.location });
            }

            return Ok(new { success = true, totalProcessed = results.Count, results });
        }


        private async Task SendReceivingStatusAsync(IEnumerable<string> serialNumbers, string owner, string location, string tag)
        {
            if (serialNumbers == null || !serialNumbers.Any()) return;

            var client = _httpClientFactory.CreateClient();
            var payload = new
            {
                serialnumbers = string.Join(",", serialNumbers),
                owner,
                location,
                tag
            };

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            await client.PostAsync("http://10.220.130.119:9090/api/RepairStatus/receiving-status", content);
        }
        }
}
