using API_WEB.ModelsDB;
using API_WEB.ModelsOracle;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace API_WEB.Controllers.Repositories
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExportController : ControllerBase
    {
        private readonly CSDL_NE _sqlContext;
        private readonly OracleDbContext _oracleContext;
        private readonly HttpClient _httpClient;

        public ExportController(CSDL_NE sqlContext, OracleDbContext oracleContext, HttpClient httpClient)
        {
            _sqlContext = sqlContext ?? throw new ArgumentNullException(nameof(sqlContext));
            _oracleContext = oracleContext ?? throw new ArgumentNullException(nameof(oracleContext));
        }

        [HttpPost("ExportSerialNumber")]
        public async Task<IActionResult> ExportSN([FromBody] RequestExport request)
        {
            if (request?.SerialNumbers == null || request.SerialNumbers.Count == 0)
            {
                return BadRequest(new { success = false, message = "SerialNumber is empty!" });
            }

            var sns = request.SerialNumbers.Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (sns.Count == 0) return BadRequest(new { success = false, message = "SerialNumber is empty after normalization!" });
            using var tx = await _sqlContext.Database.BeginTransactionAsync();
            try
            {
                var products = await _sqlContext.Products.Where(p => sns.Contains(p.SerialNumber)).ToListAsync();
                var khoOks = await _sqlContext.KhoOks.Where(o => sns.Contains(o.SERIAL_NUMBER)).ToListAsync();
                var khoScraps = await _sqlContext.KhoScraps.Where(s => sns.Contains(s.SERIAL_NUMBER)).ToListAsync();

                var productBySn = products.GroupBy(p => p.SerialNumber, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var exportEntries = new List<Export>(sns.Count);
                foreach (var sn in sns)
                {
                    productBySn.TryGetValue(sn, out var p);
                    exportEntries.Add(new Export
                    {
                        SerialNumber = sn,
                        ExportDate = DateTime.Now,
                        ExportPerson = request.ExportPerson,
                        ProductLine = p?.ProductLine,
                        EntryDate = p?.EntryDate,
                        ModelName = p?.ModelName,
                        CheckingB36R = 1
                    });
                }
                if (products.Count > 0) _sqlContext.Products.RemoveRange(products);
                if (khoOks.Count > 0) _sqlContext.KhoOks.RemoveRange(khoOks);
                if (khoScraps.Count > 0) _sqlContext.KhoScraps.RemoveRange(khoScraps);

                await _sqlContext.Exports.AddRangeAsync(exportEntries);
                await _sqlContext.SaveChangesAsync();
                await tx.CommitAsync();

                var data = exportEntries.Select(e => new
                {
                    e.Id,
                    e.SerialNumber,
                    e.ExportDate,
                    e.ExportPerson,
                    e.ProductLine,
                    e.EntryPerson,
                    e.CheckingB36R,
                    e.ModelName,
                    e.EntryDate
                });
                return Ok(new
                {
                    success = true,
                    totalRequested = sns.Count,
                    deleted = new { products.Count, khoOk = khoOks.Count, khoScrap = khoScraps.Count },
                    data
                });
            }
            catch (Exception)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi hệ thống." });
            }
        }


        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? serialNumber)
        {
            var query = _sqlContext.Exports.AsQueryable();
            if (!string.IsNullOrWhiteSpace(serialNumber))
            {
                query = query.Where(e => EF.Functions.Like(e.SerialNumber, $"%{serialNumber}%"));
            }

            var data = await query
                .OrderByDescending(e => e.Id)
                .Take(100)
                .ToListAsync();

            return Ok(new { success = true, data });
        }

        //[HttpPost]
        //public async Task<IActionResult> Create([FromBody] Export export)
        //{
        //    if (export == null || string.IsNullOrWhiteSpace(export.SerialNumber))
        //    {
        //        return BadRequest(new { success = false, message = "SerialNumber is required" });
        //    }

        //    export.ExportDate ??= DateTime.Now;
        //    _sqlContext.Exports.Add(export);
        //    await _sqlContext.SaveChangesAsync();

        //    return Ok(new { success = true, data = export });
        //}

        //[HttpPut("{id}")]
        //public async Task<IActionResult> Update(int id, [FromBody] Export update)
        //{
        //    var export = await _sqlContext.Exports.FindAsync(id);
        //    if (export == null)
        //    {
        //        return NotFound(new { success = false, message = "Export not found" });
        //    }

        //    if (!string.IsNullOrWhiteSpace(update.SerialNumber))
        //    {
        //        export.SerialNumber = update.SerialNumber;
        //    }

        //    export.ExportDate = update.ExportDate ?? export.ExportDate;
        //    export.ExportPerson = update.ExportPerson ?? export.ExportPerson;
        //    export.ProductLine = update.ProductLine ?? export.ProductLine;
        //    export.EntryDate = update.EntryDate ?? export.EntryDate;
        //    export.EntryPerson = update.EntryPerson ?? export.EntryPerson;
        //    export.ModelName = update.ModelName ?? export.ModelName;
        //    export.CheckingB36R = update.CheckingB36R ?? export.CheckingB36R;

        //    await _sqlContext.SaveChangesAsync();
        //    return Ok(new { success = true, data = export });
        //}

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var export = await _sqlContext.Exports.FindAsync(id);
            if (export == null)
            {
                return NotFound(new { success = false, message = "Export not found" });
            }

            _sqlContext.Exports.Remove(export);
            await _sqlContext.SaveChangesAsync();
            return Ok(new { success = true });
        }

        public class RequestExport
        {
            public string? ExportPerson { get; set; }
            public List<string>? SerialNumbers { get; set; }
        }
    }
}
