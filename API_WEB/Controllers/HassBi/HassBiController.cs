using System.Linq; // Cần thiết cho Linq
using System;
using API_WEB.ModelsDB; // Namespace chứa DbContext và model
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API_WEB.ModelsOracle;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;

namespace API_WEB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HassBiController : ControllerBase
    {
        private readonly CSDL_NE HassBiDB;
        private readonly OracleDbContext SamplingRecordDB;
        private readonly OracleDbContext HassBiFailData;
        private readonly OracleDbContext timeStartMoData;

        public HassBiController(CSDL_NE HassBiDB, OracleDbContext SamplingRecordDB, OracleDbContext hassBiFailData, OracleDbContext timeStartMoData)
        {
            this.HassBiDB = HassBiDB;
            this.SamplingRecordDB = SamplingRecordDB;
            this.HassBiFailData = hassBiFailData;
            this.timeStartMoData = timeStartMoData;
        }



        // API update remark
        [HttpPost("update-remark")]
        public async Task<IActionResult> UpdateRemark([FromBody] UpdateRemarkRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MoNumber) || string.IsNullOrWhiteSpace(request.Type))
            {
                return BadRequest("MoNumber và Type không được để trống.");
            }

            try
            {
                var record = await HassBiDB.HassBis
                    .FirstOrDefaultAsync(h => h.MoNumber == request.MoNumber && h.Type == request.Type);

                if (record == null)
                {
                    return NotFound($"Không tìm thấy bản ghi với MoNumber: {request.MoNumber} và Type: {request.Type}");
                }

                // Cập nhật Remark
                record.Remark = request.Remark;
                await HassBiDB.SaveChangesAsync();

                return Ok("Cập nhật Remark thành công!");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
            }
        }

        // Request model
        public class UpdateRemarkRequest
        {
            public string MoNumber { get; set; }
            public string Type { get; set; }
            public string Remark { get; set; }
        }

        // API cập nhật giá trị NvConfirm trong bảng HassBi.

        [HttpPost("update-nvconfirm")]
        public async Task<IActionResult> UpdateNvConfirm([FromBody] NvConfirmUpdateModel model)
        {
            if (string.IsNullOrWhiteSpace(model.MoNumber) || string.IsNullOrWhiteSpace(model.Type))
            {
                return BadRequest("MoNumber và Type không được để trống.");
            }

            var record = await HassBiDB.HassBis
                .FirstOrDefaultAsync(h => h.MoNumber == model.MoNumber && h.Type == model.Type);

            if (record == null)
            {
                return NotFound("Không tìm thấy dữ liệu phù hợp.");
            }

            // Cập nhật giá trị NvConfirm và ConfirmTime
            record.NvConfirm = model.NvConfirm;
            record.ConfirmTime = DateTime.Now; // Cập nhật thời gian hiện tại

            await HassBiDB.SaveChangesAsync();

            return Ok(new
            {
                message = "Cập nhật thành công!",
                confirmTime = record.ConfirmTime // Trả về thời gian cập nhật
            });
        }

        public class NvConfirmUpdateModel
        {
            public string MoNumber { get; set; }
            public string Type { get; set; }
            public string NvConfirm { get; set; }
        }

        // API HassBiFailData

        [HttpPost("get-failed-serial-numbers")]
        public async Task<IActionResult> GetFailedSerialNumbers([FromBody] SerialNumberRequestModel model)
        {
            if (string.IsNullOrWhiteSpace(model.MoNumber) ||
                string.IsNullOrWhiteSpace(model.Type) ||
                string.IsNullOrWhiteSpace(model.Status))
            {
                return BadRequest("MoNumber, Type, và Status không được để trống.");
            }

            // Truy vấn dữ liệu từ Oracle Database
            var query = @"
                SELECT 
                    a.serial_number AS SerialNumber,
                    a.model_name AS ModelName,
                    a.mo_number AS MoNumber, 
                    a.group_name AS GroupName, 
                    a.fail_time AS FailTime, 
                    b.test_code AS TestCode, 
                    b.data1 AS Data1
                    
                FROM 
                    SFISM4.R_BI_HASS_DETAIL_T a
                INNER JOIN 
                    sfism4.r109 b 
                ON 
                    a.serial_number = b.serial_number AND 
                    a.GROUP_NAME = b.TEST_GROUP
                WHERE 
                    a.MO_NUMBER = :MoNumber AND 
                    a.TYPE = :Type AND 
                    a.STATUS = :Status";

            var failedSerials = await HassBiFailData.FailedSerialNumbers
                .FromSqlRaw(query,
                    new OracleParameter("MoNumber", model.MoNumber),
                    new OracleParameter("Type", model.Type),
                    new OracleParameter("Status", model.Status))
                .ToListAsync();

            return Ok(failedSerials);
        }

        public class SerialNumberRequestModel
        {
            public string MoNumber { get; set; }
            public string Type { get; set; }
            public string Status { get; set; }
        }

        // update MO từ SFISM4_R_SAMPLING_RECORD_T sang HassBi

        [HttpPost("update-mo-status")]
        public async Task<IActionResult> UpdateMoStatus()
        {
            try
            {
                // Lấy dữ liệu từ Oracle
                var oracleRecords = await SamplingRecordDB.SamplingRecords
                    .Select(r => new
                    {
                        r.MO_NUMBER,
                        r.TYPE
                    })
                    .Distinct()
                    .ToListAsync();

                // Lấy dữ liệu từ SQL Server
                var sqlRecords = await HassBiDB.HassBis
                    .Select(h => new
                    {
                        h.MoNumber,
                        h.Type
                    })
                    .Distinct()
                    .ToListAsync();

                // Tìm các cặp MoNumber và Type bị thiếu trong HassBi
                var missingRecords = oracleRecords
                    .Where(o => !sqlRecords
                        .Any(s => s.MoNumber == o.MO_NUMBER && s.Type == o.TYPE))
                    .ToList();

                if (missingRecords.Any())
                {
                    foreach (var record in missingRecords)
                    {
                        // Lấy TimeStart từ Oracle
                        // Chuyển đổi Type
                            var typeForOracle = record.TYPE switch
                            {
                                "HASS_SAMPLING" => "HASS",
                                "BI_SAMPLING" => "BI",
                                _ => record.TYPE // Giữ nguyên nếu không phải hai giá trị trên
                            };

                        // Lấy TimeStart từ Oracle
                        var timeStart = await GetTimeStartFromOracle(record.MO_NUMBER, typeForOracle);
                        //timeStart ??= DateTime.Now; // Nếu không có TimeStart, gán giá trị hiện tại

                        if (timeStart == null)
                        {
                            // Ghi log khi không có dữ liệu trả về
                            Console.WriteLine($"Không tìm thấy TimeStart cho MO_NUMBER={record.MO_NUMBER} và TYPE={record.TYPE}");
                            continue; // Bỏ qua nếu không có giá trị TimeStart
                        }

                        // Thêm bản ghi vào bảng HassBi
                        HassBiDB.HassBis.Add(new HassBi
                        {
                            MoNumber = record.MO_NUMBER,
                            Type = record.TYPE,
                            TimeStart = timeStart.Value, // Chỉ ép kiểu khi chắc chắn không null
                            Status = "New", // Gán mặc định
                            Remark = "Auto-added"
                        });
                    }

                    // Lưu thay đổi vào SQL Server
                    try
                    {
                        await HassBiDB.SaveChangesAsync();
                        Console.WriteLine("Dữ liệu đã được lưu thành công vào SQL Server.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lỗi khi lưu dữ liệu vào SQL Server: {ex.Message}");
                        return StatusCode(500, $"Lỗi khi lưu dữ liệu: {ex.Message}");
                    }
                }

                // **Cập nhật Status cho tất cả các cặp MoNumber và Type trong HassBi**
                var hassBiRecords = await HassBiDB.HassBis.ToListAsync();

                foreach (var hassBi in hassBiRecords)
                {
                    if (hassBi.Status == "Close")
                    {
                        // Bỏ qua nếu Status đã là "Close"
                        continue;
                    }

                    if (hassBi.NvConfirm == "Approve")
                    {
                        // Nếu NvConfirm là "Approve", đặt Status thành "Close"
                        hassBi.Status = "Close";
                        continue;
                    }

                    // Lấy dữ liệu từ Oracle với MoNumber và Type
                    var oracleData = await SamplingRecordDB.SamplingRecords
                        .Where(r => r.MO_NUMBER == hassBi.MoNumber && r.TYPE == hassBi.Type)
                        .Select(r => new
                        {
                            PASS_QTY2 = r.PASS_QTY2 ?? 0,
                            SAMPLE_SIZE = r.SAMPLE_SIZE ?? 0,
                            FAIL_QTY = r.FAIL_QTY ?? 0,
                            FAIL_QTY1 = r.FAIL_QTY1 ?? 0,
                            FAIL_QTY2 = r.FAIL_QTY2 ?? 0,
                            MAJOR_REJECT_QTY = r.MAJOR_REJECT_QTY ?? 0
                        })
                        .ToListAsync();

                    if (!oracleData.Any())
                    {
                        // Nếu không tìm thấy dữ liệu trong Oracle, bỏ qua bản ghi
                        continue;
                    }

                    // Lấy bản ghi đầu tiên từ kết quả Oracle
                    var data = oracleData.FirstOrDefault();

                    // Kiểm tra các điều kiện để cập nhật Status
                    if (hassBi.Type == "HASS_SAMPLING")
                    {
                        if (data.PASS_QTY2 == data.SAMPLE_SIZE ||
                        ((data.FAIL_QTY + data.FAIL_QTY1 + data.FAIL_QTY2) <= data.MAJOR_REJECT_QTY &&
                         (data.PASS_QTY2 + data.FAIL_QTY2 == data.SAMPLE_SIZE)))
                        {
                            // PASS_QTY2 == SAMPLE_SIZE 
                            // Hoặc (FAIL_QTY + FAIL_QTY1 + FAIL_QTY2 <= MAJOR_REJECT_QTY && PASS_QTY2 + FAIL_QTY2 == SAMPLE_SIZE)
                            hassBi.Status = "Close";
                        }
                        else if (data.PASS_QTY2 < data.SAMPLE_SIZE &&
                                 (data.FAIL_QTY + data.FAIL_QTY1 + data.FAIL_QTY2) <= data.MAJOR_REJECT_QTY)
                        {
                            // Nếu FAIL_QTY + FAIL_QTY1 + FAIL_QTY2 <= MAJOR_REJECT_QTY -> Status = "On-going"
                            hassBi.Status = "On-going";
                        }
                        else if ((data.FAIL_QTY + data.FAIL_QTY1 + data.FAIL_QTY2) > data.MAJOR_REJECT_QTY)
                        {
                            // Nếu FAIL_QTY + FAIL_QTY1 + FAIL_QTY2 > MAJOR_REJECT_QTY -> Status = "Fail"
                            hassBi.Status = "Fail";
                        }
                    }

                    if (hassBi.Type == "BI_SAMPLING")
                    {
                        if (data.PASS_QTY2 == data.SAMPLE_SIZE )
                        {
                            // PASS_QTY2 == SAMPLE_SIZE 
                            // Hoặc (FAIL_QTY + FAIL_QTY1 + FAIL_QTY2 <= MAJOR_REJECT_QTY && PASS_QTY2 + FAIL_QTY2 == SAMPLE_SIZE)
                            hassBi.Status = "Close";
                        }
                        else if (data.PASS_QTY2 < data.SAMPLE_SIZE &&
                                 (data.FAIL_QTY + data.FAIL_QTY1 + data.FAIL_QTY2) == 0)
                        {
                            // Nếu FAIL_QTY + FAIL_QTY1 + FAIL_QTY2 <= MAJOR_REJECT_QTY -> Status = "On-going"
                            hassBi.Status = "On-going";
                        }
                        else if ((data.FAIL_QTY + data.FAIL_QTY1 + data.FAIL_QTY2) > 0)
                        {
                            // Nếu FAIL_QTY + FAIL_QTY1 + FAIL_QTY2 > MAJOR_REJECT_QTY -> Status = "Fail"
                            hassBi.Status = "Fail";
                        }
                    }

                    
                }

                // Lưu thay đổi vào SQL Server
                try
                {
                    await HassBiDB.SaveChangesAsync();
                    Console.WriteLine("Dữ liệu đã được lưu thành công vào SQL Server.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi khi lưu dữ liệu vào SQL Server: {ex.Message}");
                    return StatusCode(500, $"Lỗi khi lưu dữ liệu: {ex.Message}");
                }

                return Ok("Cập nhật MoNumber, Type, TimeStart và Status thành công.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi: {ex.Message}");
            }
        }

        // Hàm lấy TimeStart từ Oracle
        private async Task<DateTime?> GetTimeStartFromOracle(string moNumber, string type)
        {
            try
            {
                Console.WriteLine($"Thực hiện truy vấn TimeStart cho MO_NUMBER={moNumber} và TYPE={type}");

                var query = @"
            SELECT MIN(IN_DATE) AS EarliestDate
            FROM SFISM4.R_GET_ATE_SN_T
            WHERE MO_NUMBER = :MoNumber AND TO_GROUP = :Type";

                var parameters = new[]
                {
            new OracleParameter("MoNumber", OracleDbType.Varchar2) { Value = moNumber.Trim() },
            new OracleParameter("Type", OracleDbType.Varchar2) { Value = type.Trim() }
        };

                using (var connection = new OracleConnection("User Id=TE;Password=B05te;Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=10.220.130.220)(PORT=1521)))(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME=vnsfc)));"))
                {
                    await connection.OpenAsync();

                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.AddRange(parameters);

                        var result = await command.ExecuteScalarAsync();

                        Console.WriteLine($"Kết quả TimeStart từ Oracle cho MO_NUMBER={moNumber} và TYPE={type}: {result}");

                        // Xử lý nếu giá trị null
                        return result == DBNull.Value ? null : Convert.ToDateTime(result);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi lấy TimeStart từ Oracle: {ex.Message}");
                return null; // Trả về null nếu có lỗi
            }
        }

        public class GetAteSn
        {
            public DateTime? IN_DATE { get; set; } // Cột IN_DATE
            public string MO_NUMBER { get; set; } // Cột MO_NUMBER
            public string TO_GROUP { get; set; }  // Cột TO_GROUP
        }

        // API lấy tất cả dữ liệu 

        [HttpPost("search-all-statuses")]
        public async Task<IActionResult> SearchAllStatuses()
        {
            try
            {
                // Lấy tất cả dữ liệu từ bảng HassBi trong SQL Server
                var sqlResults = await HassBiDB.HassBis.ToListAsync();

                if (!sqlResults.Any())
                {
                    return NotFound("Không tìm thấy dữ liệu trong bảng HassBi.");
                }

                // Lấy danh sách MoNumber và Type từ kết quả SQL Server
                var moNumbers = sqlResults.Select(h => h.MoNumber).Distinct().ToList();
                var types = sqlResults.Select(h => h.Type).Distinct().ToList();

                // Tìm dữ liệu trong bảng SFISM4.R_SAMPLING_RECORD_T trong Oracle
                var oracleResults = await SamplingRecordDB.SamplingRecords
                    .Where(r => moNumbers.Contains(r.MO_NUMBER) && types.Contains(r.TYPE))
                    .Select(r => new
                    {
                        r.MO_NUMBER,
                        r.MODEL_NAME,
                        r.TYPE,
                        r.KEY_PART_NO,
                        r.TARGET_QTY,
                        r.SAMPLE_SIZE,
                        r.MAJOR_REJECT_QTY,
                        r.PASS_QTY,
                        r.FAIL_QTY,
                        r.PASS_QTY1,
                        r.FAIL_QTY1,
                        r.PASS_QTY2,
                        r.FAIL_QTY2
                    })
                    .ToListAsync();

                // Kết hợp dữ liệu từ SQL Server và Oracle
                var combinedResults = sqlResults.Select(sql => new
                {
                    sql.MoNumber,
                    sql.Type,
                    sql.TimeStart,
                    sql.Status,
                    sql.Remark,
                    sql.NvConfirm,
                    sql.ConfirmTime,
                    OracleData = oracleResults
                        .Where(o => o.MO_NUMBER == sql.MoNumber && o.TYPE == sql.Type) // Liên kết dựa trên MoNumber và Type
                        .Select(o => new
                        {
                            o.MODEL_NAME,
                            o.KEY_PART_NO,
                            o.TARGET_QTY,
                            o.SAMPLE_SIZE,
                            o.MAJOR_REJECT_QTY,
                            o.PASS_QTY,
                            o.FAIL_QTY,
                            o.PASS_QTY1,
                            o.FAIL_QTY1,
                            o.PASS_QTY2,
                            o.FAIL_QTY2
                        })
                        .ToList() // Danh sách dữ liệu liên quan từ Oracle
                });

                return Ok(combinedResults);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi lấy dữ liệu: {ex.Message}");
                return StatusCode(500, $"Lỗi: {ex.Message}");
            }
        }

        [HttpGet("get-skipped-mos")]
        public async Task<IActionResult> GetSkippedMos()
        {
            try
            {
                var query = @"
            SELECT 
                h.MODEL_NAME,
                h.MO_NUMBER,
                h.TYPE,
                h.SYSTEM_NAME,
                h.DESCRIPTION
            FROM 
                SFIS1.C_BI_HASS_CONTROL_T h
            INNER JOIN 
                SFIS1.C_MODEL_DESC_T s
            ON 
                h.MODEL_NAME = s.MODEL_NAME
            WHERE 
                h.TYPE = 'BI_HASS'
                AND s.MODEL_SERIAL = 'ADAPTER'";

                var skippedMos = new List<SkippedMo>();
                string connectionString = "User Id=TE;Password=B05te;Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=10.220.130.220)(PORT=1521)))(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME=vnsfc)))";

                using (var connection = new OracleConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                skippedMos.Add(new SkippedMo
                                {
                                    MODEL_NAME = reader["MODEL_NAME"]?.ToString(),
                                    MO_NUMBER = reader["MO_NUMBER"]?.ToString(),
                                    TYPE = reader["TYPE"]?.ToString(),
                                    SYSTEM_NAME = reader["SYSTEM_NAME"]?.ToString(),
                                    DESCRIPTION = reader["DESCRIPTION"]?.ToString()
                                });
                            }
                        }
                    }
                }

                if (!skippedMos.Any())
                {
                    return NotFound("Không tìm thấy MO nào bị Skipped.");
                }

                return Ok(skippedMos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
            }
        }

        public class SkippedMo
        {
            public string MODEL_NAME { get; set; }
            public string MO_NUMBER { get; set; }
            public string TYPE { get; set; }
            public string SYSTEM_NAME { get; set; }
            public string DESCRIPTION { get; set; }
        }
    }
}
