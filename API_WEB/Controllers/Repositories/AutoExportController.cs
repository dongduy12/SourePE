using API_WEB.ModelsDB;
using API_WEB.ModelsOracle;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
namespace API_WEB.Controllers.Repositories
{
    public class AutoExportController : Controller
    {
        private readonly CSDL_NE _sqlContext;
        private readonly OracleDbContext _oracleContext;
        public AutoExportController(CSDL_NE sqlContext, OracleDbContext oracleContext)
        {
            _sqlContext = sqlContext;
            _oracleContext = oracleContext;
        }
        [HttpPost("AutoExport")]
        public async Task<IActionResult> AutoExport()
        {
            try
            {
                // 1. Lấy danh sách Serial Numbers từ SQL Server
                var serialNumbers = await _sqlContext.Products.Select(p => p.SerialNumber).ToListAsync();
                if (!serialNumbers.Any())
                {
                    return Ok(new { success = false, message = "Không có Serial Number nào trong hệ thống SQL Server." });
                }

                // 2. Kết nối Oracle
                await using var oracleConnection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
                await oracleConnection.OpenAsync();

                // 3. Truy vấn bảng Z_KANBAN_TRACKING_T
                var wipGroups = await GetWipGroupsFromOracleAsync(oracleConnection, serialNumbers);

                // 4. Lọc SN để xuất kho dựa trên điều kiện
                var snToExport = new List<string>();

                foreach (var sn in serialNumbers)
                {
                    // Lấy sản phẩm từ bảng Products trong SQL Server
                    var product = await _sqlContext.Products.FirstOrDefaultAsync(p => p.SerialNumber == sn);
                    if (product == null || product.BorrowStatus == "Available")
                    {
                        continue;
                    }
                    if (!wipGroups.ContainsKey(sn))
                    {
                        // TH1: Không có kết quả trả về từ Z_KANBAN_TRACKING_T
                        var errorFlag = await GetErrorFlagFromR107Async(oracleConnection, sn);
                        if (errorFlag == "0" || errorFlag == "1")
                        {
                            snToExport.Add(sn);
                        }
                    }
                    //else if (wipGroups[sn] == "B36R_TO_SFG")
                    //{
                    //    // TH2: WIP_GROUP = "B36R_TO_SFG", kiểm tra thêm trong bảng R109
                    //    var errorFlag = await GetErrorFlagFromR107Async(oracleConnection, sn);
                    //    if (errorFlag == "0" || errorFlag == "1")
                    //    {
                    //        snToExport.Add(sn);
                    //    }
                    //}
                }
                if (!snToExport.Any())
                {
                    return Ok(new { success = false, message = "Không có SN nào thỏa mãn điều kiện xuất kho." });
                }
                // 5. Cập nhật trạng thái xuất kho vào SQL Server

                var exports = new List<Export>();
                foreach (var sn in snToExport)
                {
                    var product = await _sqlContext.Products.FirstOrDefaultAsync(p => p.SerialNumber == sn);
                    if (product != null)
                    {
                        exports.Add(new Export
                        {
                            SerialNumber = product.SerialNumber,
                            ExportDate = DateTime.Now,
                            ExportPerson = "Auto_Export",
                            ProductLine = product.ProductLine,
                            EntryDate = product.EntryDate,
                            EntryPerson = product.EntryPerson,
                            ModelName = product.ModelName
                        });
                    }
                }

                // Lưu vào bảng Export
                await _sqlContext.Exports.AddRangeAsync(exports);

                // 6. Xóa các SN đã xuất khỏi bảng Products
                var productsToRemove = _sqlContext.Products.Where(p => snToExport.Contains(p.SerialNumber));
                _sqlContext.Products.RemoveRange(productsToRemove);

                await _sqlContext.SaveChangesAsync();

                // 7. Trả về kết quả
                return Ok(new
                {
                    success = true,
                    totalExported = snToExport.Count,
                    exportedSerialNumbers = snToExport
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"System Error: {ex.Message}");
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        //================================= Helper Methods ================================

        // Lấy WIP_GROUP từ Z_KANBAN_TRACKING_T
        private async Task<Dictionary<string, string>> GetWipGroupsFromOracleAsync(OracleConnection connection, List<string> serialNumbers)
        {
            var wipGroups = new Dictionary<string, string>();

            if (!serialNumbers.Any()) return wipGroups;

            var batchSize = 1000; // Chia batch để tránh lỗi ORA-01795
            for (var i = 0; i < serialNumbers.Count; i += batchSize)
            {
                var batch = serialNumbers.Skip(i).Take(batchSize).ToList();
                var serialList = string.Join(",", batch.Select(sn => $"'{sn}'"));

                var query = $@"
            SELECT SERIAL_NUMBER, WIP_GROUP 
            FROM SFISM4.Z_KANBAN_TRACKING_T
            WHERE SERIAL_NUMBER IN ({serialList})";

                using var command = new OracleCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var serialNumber = reader["SERIAL_NUMBER"]?.ToString();
                    var wipGroup = reader["WIP_GROUP"]?.ToString();
                    if (!string.IsNullOrEmpty(serialNumber) && !wipGroups.ContainsKey(serialNumber))
                    {
                        wipGroups.Add(serialNumber, wipGroup);
                    }
                }
            }

            return wipGroups;
        }

        // Lấy error_flag từ bảng R107
        private async Task<string> GetErrorFlagFromR107Async(OracleConnection connection, string serialNumber)
        {
            var query = @"
                SELECT ERROR_FLAG 
                FROM SFISM4.R107 
                WHERE SERIAL_NUMBER = :serialNumber";

            using var command = new OracleCommand(query, connection);
            command.Parameters.Add(new OracleParameter("serialNumber", OracleDbType.Varchar2) { Value = serialNumber });

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return reader["ERROR_FLAG"]?.ToString();
            }

            return null;
        }

        // Lấy thông tin MO_NUMBER và WIP_GROUP từ bảng R107
        private async Task<Dictionary<string, (string? MoNumber, string? WipGroup)>> GetR107InfoAsync(OracleConnection connection, List<string> serialNumbers)
        {
            var infos = new Dictionary<string, (string?, string?)>();

            if (!serialNumbers.Any()) return infos;

            var batchSize = 1000; // Tránh lỗi ORA-01795
            for (var i = 0; i < serialNumbers.Count; i += batchSize)
            {
                var batch = serialNumbers.Skip(i).Take(batchSize).ToList();
                var serialList = string.Join(",", batch.Select(sn => $"'{sn}'"));

                var query = $@"
            SELECT SERIAL_NUMBER, MO_NUMBER, WIP_GROUP
            FROM SFISM4.R107
            WHERE SERIAL_NUMBER IN ({serialList})";

                using var command = new OracleCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var serialNumber = reader["SERIAL_NUMBER"]?.ToString();
                    var moNumber = reader["MO_NUMBER"]?.ToString();
                    var wipGroup = reader["WIP_GROUP"]?.ToString();
                    if (!string.IsNullOrEmpty(serialNumber) && !infos.ContainsKey(serialNumber))
                    {
                        infos.Add(serialNumber, (moNumber, wipGroup));
                    }
                }
            }

            return infos;
        }

        public async Task CheckLinkMoAsync()
        {
            var exports = await _sqlContext.Exports
                .Where(e => e.CheckingB36R > 0).ToListAsync();
            if (!exports.Any())
            {
                return;
            }

            var serialNumbers = exports.Select(e => e.SerialNumber).Distinct().ToList();

            await using var oracleConnection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
            await oracleConnection.OpenAsync();
            var wipGroups = await GetWipGroupsFromOracleAsync(oracleConnection, serialNumbers);
            var r107Infos = await GetR107InfoAsync(oracleConnection, serialNumbers);

            foreach (var exp in exports)
            {
                var sn = exp.SerialNumber;
                var wipGroup = wipGroups.ContainsKey(sn) ? wipGroups[sn] : null;
                var r107Info = r107Infos.ContainsKey(sn) ? r107Infos[sn] : (null, null);
                var moNumber = r107Info.MoNumber;
                var wipGroupR107 = r107Info.WipGroup;

                var linked = false;
                if (!string.IsNullOrEmpty(wipGroup) && wipGroup.Contains("B36R_TO_SFG"))
                {
                    if (!string.IsNullOrEmpty(wipGroupR107) && wipGroupR107.Contains("REPAIR_B36R"))
                    {
                        linked = true;
                    }
                    else if (!(!string.IsNullOrEmpty(wipGroupR107) && wipGroupR107.Contains("B36R")))
                    {
                        linked = true;
                    }
                }
                else if (!string.IsNullOrEmpty(wipGroup) &&
                         (wipGroup.Contains("KANBAN_IN") || wipGroup.Contains("KANBAN_OUT")))
                {
                    if (!exp.KanbanTime.HasValue)
                    {
                        exp.KanbanTime = DateTime.Now;
                    }
                    exp.CheckingB36R = 3;
                }

                if (linked)
                {
                    if (!exp.LinkTime.HasValue)
                    {
                        exp.LinkTime = DateTime.Now;
                    }
                    exp.CheckingB36R = 2;
                }
            }

            await _sqlContext.SaveChangesAsync();
        }


        [HttpGet("checking-b36r")]
        public async Task<IActionResult> CheckingB36R(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var query = _sqlContext.Exports
                    .Where(e => e.CheckingB36R == 1 || e.CheckingB36R == 2);

                if (startDate.HasValue)
                {
                    query = query.Where(e =>
                        (e.CheckingB36R == 1 && e.ExportDate >= startDate.Value) ||
                        (e.CheckingB36R == 2 && e.LinkTime >= startDate.Value));
                }

                if (endDate.HasValue)
                {
                    query = query.Where(e =>
                        (e.CheckingB36R == 1 && e.ExportDate <= endDate.Value) ||
                        (e.CheckingB36R == 2 && e.LinkTime <= endDate.Value));
                }

                var exports = await query
                    .GroupBy(e => e.SerialNumber)
                    .Select(g => g.OrderByDescending(x => x.ExportDate)
                        .ThenByDescending(x => x.LinkTime)
                        .First())
                    .ToListAsync();

                var awaiting = exports
                    .Where(e => e.CheckingB36R == 1)
                    .Select(e => new
                    {
                        SN = e.SerialNumber,
                        ProductLine = e.ProductLine,
                        ModelName = e.ModelName,
                        ExportDate = e.ExportDate.HasValue ? e.ExportDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : "",
                        LinkTime = e.LinkTime.HasValue ? e.LinkTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "",
                        Status = "Chờ Link MO"
                    })
                    .ToList();

                var linked = exports
                    .Where(e => e.CheckingB36R == 2)
                    .Select(e => new
                    {
                        SN = e.SerialNumber,
                        ProductLine = e.ProductLine,
                        ModelName = e.ModelName,
                        ExportDate = e.ExportDate.HasValue ? e.ExportDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : "",
                        LinkTime = e.LinkTime.HasValue ? e.LinkTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "",
                        Status = "Đã link MO"
                    })
                    .ToList();

                if (!awaiting.Any() && !linked.Any())
                {
                    return Ok(new { success = false, message = "Không có Serial Number nào được đánh dấu B36R." });
                }

                return Ok(new
                {
                    success = true,
                    awaitingLinkCount = awaiting.Count,
                    linkCount = linked.Count,
                    awaiting,
                    linked,
                    message = "Thống kê Link MO."
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"System Error: {ex.Message}");
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }
    }
}
