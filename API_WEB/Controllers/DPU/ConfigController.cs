#nullable disable
using API_WEB.ModelsDB;
using API_WEB.ModelsOracle;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using Oracle.ManagedDataAccess.Client;
using System.Data.SqlTypes;
using System.Diagnostics;

namespace API_WEB.Controllers.DPU
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConfigController : ControllerBase
    {
        private readonly CSDL_NE _sqlContext;
        private readonly OracleDbContext _oracleContext;

        public ConfigController(CSDL_NE sqlContext, OracleDbContext oracleContext)
        {
            _sqlContext = sqlContext;
            _oracleContext = oracleContext;
        }

        [HttpPost("SyncSerialNumbersFromOracle")]
        public async Task<IActionResult> SyncSerialNumbersFromOracle()
        {
            try
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                string sqlServerConnectionString = _sqlContext.Database.GetDbConnection().ConnectionString;

                // Fetch Serial Numbers from Oracle
                var oracleSNs = new List<dynamic>();
                using (var oracleConnection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString))
                {
                    await oracleConnection.OpenAsync();
                    string oracleQuery = @"
                SELECT
                    a.serial_number AS SerialNumber,
                    a.model_name AS ModelName,
                    a.test_code AS TestCode,
                    a.TEST_TIME AS TestTime,
                    a.Data1 AS Data1,
                    a.TEST_GROUP AS TestGroup,
                    b.WIP_GROUP AS WIPGroup
                FROM sfism4.r109 a
                INNER JOIN sfism4.r107 b
                ON a.serial_number = b.serial_number
                WHERE a.test_code LIKE '4-1150%' AND a.reason_code IS NULL";

                    using (var command = new OracleCommand(oracleQuery, oracleConnection))
                    {
                        command.CommandTimeout = 300;
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                oracleSNs.Add(new
                                {
                                    SerialNumber = reader["SerialNumber"]?.ToString(),
                                    ModelName = reader["ModelName"]?.ToString(),
                                    TestCode = reader["TestCode"]?.ToString(),
                                    TestTime = reader["TestTime"] != DBNull.Value ? Convert.ToDateTime(reader["TestTime"]) : (DateTime?)null,
                                    Data1 = reader["Data1"]?.ToString(),
                                    TestGroup = reader["TestGroup"]?.ToString(),
                                    WIPGroup = reader["WIPGroup"]?.ToString()
                                });
                            }
                        }
                    }
                }

                if (!oracleSNs.Any())
                {
                    return NotFound(new { success = false, message = "Không có Serial Numbers trong Oracle để đồng bộ." });
                }

                using (var sqlConnection = new SqlConnection(sqlServerConnectionString))
                {
                    await sqlConnection.OpenAsync();

                    // Fetch existing Serial Numbers from DPUCurrent
                    var existingSNs = new List<string>();
                    string selectQuery = "SELECT SerialNumber FROM DPUCurrent";
                    using (var command = new SqlCommand(selectQuery, sqlConnection))
                    {
                        command.CommandTimeout = 300;
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                existingSNs.Add(reader["SerialNumber"].ToString());
                            }
                        }
                    }

                    // Identify Serial Numbers to delete and move them to DPUHistory
                    var snToDelete = existingSNs.Except(oracleSNs.Select(sn => sn.SerialNumber)).ToList();
                    if (snToDelete.Any())
                    {
                        // Insert deleted SNs into DPUHistory
                        string insertHistoryQuery = @"
                INSERT INTO DPUHistory (
                    SerialNumber, ModelName, TestCode, Data1, WIPGroup, TestGroup, TestTime, Station
                )
                SELECT SerialNumber, ModelName, TestCode, Data1, WIPGroup, TestGroup, TestTime, Station
                FROM DPUCurrent
                WHERE SerialNumber IN (SELECT value FROM STRING_SPLIT(@SerialNumbers, ','))";

                        using (var insertHistoryCommand = new SqlCommand(insertHistoryQuery, sqlConnection))
                        {
                            insertHistoryCommand.CommandTimeout = 300;
                            insertHistoryCommand.Parameters.AddWithValue("@SerialNumbers", string.Join(",", snToDelete));
                            await insertHistoryCommand.ExecuteNonQueryAsync();
                        }

                        // Delete from DPUCurrent
                        string deleteQuery = @"
                DELETE FROM DPUCurrent
                WHERE SerialNumber IN (SELECT value FROM STRING_SPLIT(@SerialNumbers, ','))";

                        using (var deleteCommand = new SqlCommand(deleteQuery, sqlConnection))
                        {
                            deleteCommand.CommandTimeout = 300;
                            deleteCommand.Parameters.AddWithValue("@SerialNumbers", string.Join(",", snToDelete));
                            await deleteCommand.ExecuteNonQueryAsync();
                        }
                    }

                    // Insert or update Serial Numbers from Oracle into DPUCurrent
                    foreach (var sn in oracleSNs)
                    {
                        string insertOrUpdateQuery = @"
                MERGE DPUCurrent AS target
                USING (SELECT @SerialNumber AS SerialNumber) AS source
                ON target.SerialNumber = source.SerialNumber
                WHEN MATCHED THEN
                    UPDATE SET ModelName = @ModelName, TestCode = @TestCode, Data1 = @Data1, WIPGroup = @WIPGroup, TestGroup = @TestGroup, TestTime = @TestTime
                WHEN NOT MATCHED THEN
                    INSERT (SerialNumber, ModelName, TestCode, Data1, WIPGroup, TestGroup, TestTime, Station)
                    VALUES (@SerialNumber, @ModelName, @TestCode, @Data1, @WIPGroup, @TestGroup, @TestTime, 'DDR-TOOL');";

                        using (var command = new SqlCommand(insertOrUpdateQuery, sqlConnection))
                        {
                            command.CommandTimeout = 300;
                            command.Parameters.AddWithValue("@SerialNumber", sn.SerialNumber ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("@ModelName", sn.ModelName ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("@TestCode", sn.TestCode ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("@Data1", sn.Data1 ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("@WIPGroup", sn.WIPGroup ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("@TestGroup", sn.TestGroup ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("@TestTime", sn.TestTime ?? (object)DBNull.Value);
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                }

                stopwatch.Stop();
                return Ok(new { success = true, message = "Đồng bộ thành công Serial Numbers từ Oracle sang SQL Server." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi đồng bộ dữ liệu.", error = ex.Message });
            }
        }

        [HttpPost("ProcessSNFlow")]
        public async Task<IActionResult> ProcessSNFlow([FromBody] List<ProcessSNRequest> requests)
        {
            if (requests == null || !requests.Any())
            {
                return BadRequest(new { success = false, message = "Vui lòng nhập Serial Numbers!" });
            }
            var errors = new List<string>();
            using var transaction = await _sqlContext.Database.BeginTransactionAsync();
            try
            {
                foreach (var request in requests)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(request.SerialNumber))
                        {
                            errors.Add("SerialNumber không được để trống.");
                            continue;
                        }

                        // Lấy bản ghi mới nhất cho SerialNumber
                        var current = await _sqlContext.DPUCurrent
                            .OrderByDescending(x => x.CurrentID)
                            .FirstOrDefaultAsync(x => x.SerialNumber == request.SerialNumber);

                        if (current == null)
                        {
                            errors.Add($"SN {request.SerialNumber} không tồn tại trong database!");
                            continue;
                        }

                        // Logic đặc biệt cho trạm CUSTOMER
                        if (current.Station == "CUSTOMER")
                        {
                            if (string.IsNullOrWhiteSpace(request.NextStation))
                            {
                                errors.Add($"SN {request.SerialNumber}: NextStation không được để trống khi Station là CUSTOMER.");
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(request.CustomerInput))
                            {
                                errors.Add($"SN {request.SerialNumber}: Giá trị nhập cho cột Customer không được để trống.");
                                continue;
                            }

                            // Lưu giá trị người dùng nhập vào cột Customer
                            current.NVInstruction = string.IsNullOrWhiteSpace(current.NVInstruction)
                                ? request.CustomerInput
                                : $"{current.NVInstruction},{request.CustomerInput}";

                            // Cập nhật NextStation vào cột Station
                            current.PreviousStation = current.Station;
                            current.Station = request.NextStation;
                            current.OwnerNe = request.Owner;

                            _sqlContext.DPUCurrent.Update(current);
                            continue; // Skip logic xử lý trạng thái khác
                        }

                        if (current.Station == "REPAIR")
                        {
                            if (string.IsNullOrWhiteSpace(request.RERepairInput))
                            {
                                errors.Add($"SN {request.SerialNumber}: Vui lòng nhập vị trí sửa chữa!!");
                                continue;
                            }

                            current.Repair = string.IsNullOrWhiteSpace(current.Repair)
                                ? request.RERepairInput
                                : $"{current.Repair},{request.RERepairInput}";

                            current.PreviousStation = current.Station;
                            current.Station = "FT-OFF";
                            _sqlContext.DPUCurrent.Update(current);

                            var repairHistory = await _sqlContext.RepairHistory.Include(rh => rh.RepairActions).FirstOrDefaultAsync(rh => rh.SerialNumber == request.SerialNumber);
                            if(repairHistory == null)
                            {
                                repairHistory = new RepairHistory
                                {
                                    SerialNumber = request.SerialNumber,
                                    ModelName = current.ModelName,
                                    ProductLine = current.WIPGroup,
                                    RepairTime = DateTime.Now,
                                    CreatedAt = DateTime.Now,
                                    RepairActions = new List<RepairAction>()
                                };
                                _sqlContext.RepairHistory.Add(repairHistory);
                            }
                            var repairAction = new RepairAction
                            {
                                ActionDescription = request.RERepairInput,
                                ActionTime = DateTime.Now,
                                ResponsiblePerson = request.Owner,
                                Data1 = current.Data1,
                                Note = ""
                            };
                            repairHistory.RepairActions.Add(repairAction);
                            continue;
                        }

                        // Lấy trạng thái của trạm trước đó
                        var previousStationStatus = await _sqlContext.DPUCurrent
                            .Where(x => x.SerialNumber == request.SerialNumber)
                            .OrderByDescending(x => x.CurrentID)
                            .Select(x => x.DDRTOOL) // Tùy thuộc vào cột trạng thái
                            .FirstOrDefaultAsync();

                        if (request.Status == "PASS")
                        {
                            // Cập nhật PASS cho cột tương ứng với station
                            switch (current.Station)
                            {
                                case "DDR-TOOL":
                                    current.DDRTOOL = "PASS";
                                    break;
                                case "FT-OFF":
                                    current.FTOFF = "PASS";
                                    break;
                                case "HASS":
                                    current.HASS = "PASS";
                                    break;
                                case "FT-HASS":
                                    current.FTHASS = "PASS";
                                    break;
                                default:
                                    errors.Add($"SN {request.SerialNumber}: Station không hỗ trợ cập nhật PASS.");
                                    continue;
                            }

                            current.PreviousStation = current.Station;
                            current.Station = GetNextStation(current.Station, request.Status, previousStationStatus);
                            current.OwnerNe = request.Owner;
                            _sqlContext.DPUCurrent.Update(current);
                            continue;
                        }

                        // Xử lý các Station khác nếu có trạng thái FAIL
                        if (request.Status == "FAIL" || request.Status == "HANG UP")
                        {
                            switch (current.Station)
                            {
                                case "DDR-TOOL":
                                    if (request.Status == "HANG UP")
                                    {
                                        // Chuyển Station về CUSTOMER nếu Status là HANG UP
                                        current.PreviousStation = current.Station;
                                        current.Station = "CUSTOMER";
                                        current.OwnerNe = request.Owner;
                                        _sqlContext.DPUCurrent.Update(current);
                                        continue; // Bỏ qua các xử lý tiếp theo
                                    }
                                    if (!string.IsNullOrWhiteSpace(request.DDRToolFailCode))
                                    {
                                        current.DDRTOOL = string.IsNullOrWhiteSpace(current.DDRTOOL)
                                            ? request.DDRToolFailCode
                                            : $"{current.DDRTOOL},{request.DDRToolFailCode}";

                                        var ddrToolFails = current.DDRTOOL.Split(',')
                                            .Where(x => !string.IsNullOrWhiteSpace(x))
                                            .ToList();
                                        if (ddrToolFails.Count < 3)
                                        {
                                            current.PreviousStation = current.Station;
                                            current.Station = "REPAIR";
                                            current.OwnerNe = request.Owner;
                                            _sqlContext.DPUCurrent.Update(current);
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        errors.Add($"SN {request.SerialNumber}: Giá trị FAIL cho DDR-TOOL không được rỗng!");
                                        continue;
                                    }
                                    break;

                                case "FT-OFF":
                                    if (!string.IsNullOrWhiteSpace(request.FT_OFFFailCode))
                                    {
                                        current.FTOFF = string.IsNullOrWhiteSpace(current.FTOFF)
                                            ? request.FT_OFFFailCode
                                            : $"{current.FTOFF},{request.FT_OFFFailCode}";

                                        var newRecord = new DPUCurrent
                                        {
                                            SerialNumber = current.SerialNumber,
                                            Data1 = current.Data1,
                                            ModelName = current.ModelName,
                                            TestGroup = current.TestGroup,
                                            WIPGroup = current.WIPGroup,
                                            DDRTOOL = null,
                                            HASS = null,
                                            FTHASS = null,
                                            Station = "DDR-TOOL",
                                            PreviousStation = current.Station,
                                            TestTime = DateTime.Now,
                                            OwnerNe = request.Owner
                                        };
                                        await _sqlContext.DPUCurrent.AddAsync(newRecord);
                                    }
                                    else
                                    {
                                        errors.Add($"SN {request.SerialNumber}: Giá trị FAIL cho FT-OFF không được rỗng!");
                                        continue;
                                    }
                                    break;

                                default:
                                    errors.Add($"SN {request.SerialNumber}: Station không hợp lệ hoặc không hỗ trợ.");
                                    continue;
                            }
                        }

                        // Cập nhật trạm và trạng thái
                        current.PreviousStation = current.Station;
                        current.Station = GetNextStation(current.Station, request.Status, previousStationStatus);
                        current.OwnerNe = request.Owner;

                        _sqlContext.DPUCurrent.Update(current);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Lỗi xảy ra khi xử lý SN: {request.SerialNumber}. Chi tiết: {ex.Message}");
                    }
                }

                await _sqlContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Lỗi nghiêm trọng xảy ra khi xử lý yêu cầu.", error = ex.Message });
            }

            if (errors.Any())
            {
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi xử lý một số SN.", errors });
            }
            return Ok(new { success = true, message = "Cập nhật thành công tất cả các SN." });
        }
        private string GetNextStation(string currentStation, string status, string previousStationStatus)
        {
            // Quy trình chuyển trạm dựa vào station hiện tại, trạng thái hiện tại và trạng thái DDR-TOOL trước đó
            if (currentStation == "DDR-TOOL")
            {
                if (status == "PASS")
                {
                    return "FT-OFF"; // Nếu DDR-TOOL PASS, chuyển sang FT-OFF
                }
                return "CUSTOMER"; // Nếu DDR-TOOL FAIL, chuyển sang CUSTOMER
            }
            else if (currentStation == "CUSTOMER")
            {
                return "REPAIR"; // Luôn chuyển sang REPAIR
            }
            else if (currentStation == "REPAIR")
            {
                return "FT-OFF"; // Luôn chuyển sang FT-OFF
            }
            else if (currentStation == "FT-OFF")
            {
                if (status == "PASS")
                {
                    // Nếu FT-OFF PASS, kiểm tra trạng thái DDR-TOOL trước đó
                    if (previousStationStatus == "PASS")
                    {
                        return "HASS"; // Nếu DDR-TOOL PASS, chuyển sang HASS
                    }
                    return "WAITTING ONLINE"; // Nếu DDR-TOOL FAIL, chuyển sang ICT
                }
                return "DDR-TOOL"; // Nếu FT-OFF FAIL, quay lại DDR-TOOL
            }
            else if (currentStation == "HASS")
            {
                if (status == "PASS")
                {
                    return "FT-HASS"; // Nếu HASS PASS, chuyển sang FT-HASS
                }
                return "CUSTOMER"; // Nếu HASS FAIL, quay lại CUSTOMER
            }
            else if (currentStation == "FT-HASS")
            {
                return "WATTING ONLINE"; // Luôn chuyển sang ONLINE
            }

            return "DDR-TOOL"; // Mặc định quay lại DDR-TOOL nếu không xác định được trạm
        }


        [HttpPost("SearchSNHistory")]
        public async Task<IActionResult> SearchSNHistory([FromBody] List<string> serialNumbers)
        {
            if (serialNumbers == null || !serialNumbers.Any())
            {
                return BadRequest(new { success = false, message = "Vui lòng nhập danh sách Serial Numbers." });
            }

            try
            {
                // Lấy thông tin lịch sử từ DPUHistory
                var historyInfo = await _sqlContext.DPUHistory
                    .Where(x => serialNumbers.Contains(x.SerialNumber))
                    .Select(x => new
                    {
                        SerialNumber = x.SerialNumber ?? "",
                        ModelName = x.ModelName ?? "",
                        TestCode = x.TestCode ?? "",
                        Data1 = x.Data1 ?? string.Empty,
                        WIPGroup = x.WIPGroup ?? string.Empty,
                        TestTime = x.TestTime ?? DateTime.MinValue,
                        Station = x.Station ?? "",
                        PreviousStation = x.PreviousStation ?? "",
                        TestGroup = x.TestGroup ?? "",
                        HASSFail = x.HASS ?? "",
                        FTOffFail = x.FTOFF ?? "",
                        FTHASSFail = x.FTHASS ?? "",
                        DDRToolFail = x.DDRTOOL ?? "",
                        CustomerInput = x.NVInstruction ?? "",
                        RERepairInput = x.Repair ?? "",
                        PEInstruction = x.PEInstruction ?? "",
                        Owner = x.OwnerNe ?? ""
                    })
                    .ToListAsync();

                // Kiểm tra nếu không có dữ liệu
                if (!historyInfo.Any())
                {
                    return NotFound(new { success = false, message = "Không tìm thấy thông tin lịch sử cho các Serial Numbers được cung cấp." });
                }

                return Ok(new
                {
                    success = true,
                    history = historyInfo
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi xử lý yêu cầu." });
            }
        }

        [HttpPost("SearchSNCurrent")]
        public async Task<IActionResult> SearchSNCurrent([FromBody] List<string> serialNumbers)
        {
            if (serialNumbers == null || !serialNumbers.Any())
            {
                return BadRequest(new { success = false, message = "Vui long nhap SN!!!!" });
            }

            try
            {
                // Lấy thông tin hiện tại từ DPUCurrent
                var currentInfo = await _sqlContext.DPUCurrent
                    .Where(x => serialNumbers.Contains(x.SerialNumber))
                    .Select(x => new
                    {
                        SerialNumber = x.SerialNumber ?? "",
                        ModelName = x.ModelName ?? "",
                        TestCode = x.TestCode ?? "",
                        Data1 = x.Data1 ?? string.Empty,
                        WIPGroup = x.WIPGroup ?? string.Empty,
                        TestTime = x.TestTime ?? DateTime.MinValue,
                        Station = x.Station ?? "",
                        PreviousStation = x.PreviousStation ?? "",
                        TestGroup = x.TestGroup??"",
                        HASSFail = x.HASS ?? "",
                        FTOffFail = x.FTOFF ?? "",
                        FTHASSFail = x.FTHASS ?? "",
                        DDRToolFail = x.DDRTOOL ?? "",
                        CustomerInput = x.NVInstruction ?? "",
                        RERepairInput = x.Repair ?? "",
                        PEInstruction = x.PEInstruction ?? "",
                        Owner = x.OwnerNe??""
                                                                      
                    }).ToListAsync();

                if (currentInfo == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy thông tin hiện tại cho SerialNumber." });
                }


                return Ok(new
                {
                    success = true,
                    current = currentInfo
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("UpdateInstruction")]
        public async Task<IActionResult> UpdateInstruction([FromBody] List<UpdateRequest> requests)
        {
            if (requests == null || !requests.Any())
            {
                return BadRequest(new { success = false, message = "Danh sách Serial Numbers không được rỗng." });
            }

            foreach (var request in requests)
            {
                var currentRecord = await _sqlContext.DPUCurrent
                    .Where(x => x.SerialNumber == request.SerialNumber)
                    .FirstOrDefaultAsync();

                if (currentRecord != null)
                {
                    if (!string.IsNullOrWhiteSpace(request.PEInstruction))
                    {
                        currentRecord.PEInstruction = request.PEInstruction;
                    }
                    if (!string.IsNullOrWhiteSpace(request.NVInstruction))
                    {
                        currentRecord.NVInstruction = request.NVInstruction;
                    }
                    if (!string.IsNullOrWhiteSpace(request.RERepair))
                    {
                        currentRecord.Repair = request.RERepair;
                    }
                    _sqlContext.DPUCurrent.Update(currentRecord);
                }

                var latestHistoryRecord = await _sqlContext.DPUHistory
                    .Where(x => x.SerialNumber == request.SerialNumber)
                    .OrderByDescending(x => x.TestTime)
                    .FirstOrDefaultAsync();

                if (latestHistoryRecord != null)
                {
                    if (!string.IsNullOrWhiteSpace(request.PEInstruction))
                    {
                        latestHistoryRecord.PEInstruction = request.PEInstruction;
                    }
                    if (!string.IsNullOrWhiteSpace(request.RERepair))
                    {
                        latestHistoryRecord.Repair = request.RERepair;
                    }
                    if (!string.IsNullOrWhiteSpace(request.NVInstruction))
                    {
                        latestHistoryRecord.NVInstruction = request.NVInstruction;
                    }
                    _sqlContext.DPUHistory.Update(latestHistoryRecord);
                }
            }

            await _sqlContext.SaveChangesAsync();

            return Ok(new { success = true, message = "Cập nhật thành công!" });
        }

        [HttpGet("GetStation")]
        public async Task<IActionResult> GetStation()
        {
            try
            {
                var stationStatics = await _sqlContext.DPUCurrent.GroupBy(d => d.Station).Select(g => new
                {
                    Station = g.Key,
                    Count = g.Count()
                }).ToListAsync();
                if (!stationStatics.Any())
                {
                    return NotFound(new { success = false, message = "No data Station!!" });
                }
                return Ok(new
                {
                    success = true,
                    message = "Done!!",
                    data = stationStatics
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Xay ra loi!!" });
            }
        }

        [HttpPost("GetSNByStation")]
        public async Task<IActionResult> GetSNByStation([FromBody] List<string> station)
        {
            if (station == null || !station.Any())
            {
                return BadRequest(new { success = false, message = "Station không được để trống." });
            }

            try
            {
                // Lấy dữ liệu từ cơ sở dữ liệu trước, sau đó xử lý GroupBy trên bộ nhớ
                var dpuData = await _sqlContext.DPUCurrent
                    .Where(x => station.Contains(x.Station))
                    .ToListAsync(); // Lấy toàn bộ dữ liệu phù hợp với Station

                // Xử lý GroupBy trên bộ nhớ để lấy bản ghi mới nhất theo SerialNumber
                var groupedData = dpuData
                    .GroupBy(x => x.SerialNumber)
                    .Select(g => g.OrderByDescending(x => x.CurrentID).FirstOrDefault())
                    .ToList();

                // Chuyển đổi sang dữ liệu cần thiết để trả về
                var result = groupedData.Select(x => new
                {
                    SerialNumber = x.SerialNumber ?? "",
                    ModelName = x.ModelName ?? "",
                    TestCode = x.TestCode ?? "",
                    Data1 = x.Data1 ?? string.Empty,
                    WIPGroup = x.WIPGroup ?? string.Empty,
                    TestTime = x.TestTime ?? DateTime.MinValue,
                    Station = x.Station ?? "",
                    PreviousStation = x.PreviousStation ?? "",
                    TestGroup = x.TestGroup ?? "",
                    HASSFail = x.HASS ?? "",
                    FTOffFail = x.FTOFF ?? "",
                    FTHASSFail = x.FTHASS ?? "",
                    DDRToolFail = x.DDRTOOL ?? "",
                    CustomerInput = x.NVInstruction ?? "",
                    RERepairInput = x.Repair ?? "",
                    PEInstruction = x.PEInstruction ?? ""
                }).ToList();

                // Kiểm tra nếu không có dữ liệu
                if (!result.Any())
                {
                    return NotFound(new { success = false, message = "Không tìm thấy dữ liệu cho Station này." });
                }

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi xử lý yêu cầu.", error = ex.Message });
            }
        }

        [HttpPost("UpdateValue")]
        public async Task<IActionResult> UpdateValue([FromBody] UpdateValueRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SerialNumber))
            {
                return BadRequest(new { success = false, message = "Serial Number không được để trống." });
            }

            try
            {
                var currentRecord = await _sqlContext.DPUCurrent
                    .OrderByDescending(x => x.CurrentID)
                    .FirstOrDefaultAsync(x => x.SerialNumber == request.SerialNumber);

                if (currentRecord == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy Serial Number trong database." });
                }

                // Cập nhật giá trị theo cột
                switch (request.ColumnIndex)
                {
                    case 8: // Cột DDR-TOOL
                        currentRecord.DDRTOOL = request.NewValue;
                        break;
                    case 9: // Cột NVInstruction
                        currentRecord.NVInstruction = request.NewValue;
                        break;
                    case 10: // Cột PEInstruction
                        currentRecord.PEInstruction = request.NewValue;
                        break;        
                    case 11: // Cột RERepair
                        currentRecord.Repair = request.NewValue;
                        break;
                    case 12: // Cột FT-OFF
                        currentRecord.FTOFF = request.NewValue;
                        break;
                    case 13: // Cột HASS
                        currentRecord.HASS = request.NewValue;
                        break;
                    case 14: // Cột FT-HASS
                        currentRecord.FTHASS = request.NewValue;
                        break;
                    case 15: // 
                        currentRecord.PreviousStation = request.NewValue;
                        break;case 16: //
                        currentRecord.Station = request.NewValue;
                        break;
                    default:
                        return BadRequest(new { success = false, message = "Cột không hợp lệ." });
                }

                _sqlContext.DPUCurrent.Update(currentRecord);
                await _sqlContext.SaveChangesAsync();

                return Ok(new { success = true, message = "Cập nhật giá trị thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi cập nhật.", error = ex.Message });
            }
        }

        [HttpGet("ExportCurrentToExcel")]
        public async Task<IActionResult> ExportCurrentToExcel()
        {
            try
            {
                // Thiết lập LicenseContext
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                // Lấy toàn bộ dữ liệu từ bảng DPUCurrent
                var data = await _sqlContext.DPUCurrent
                    .OrderBy(x => x.SerialNumber)
                    .ToListAsync();

                if (!data.Any())
                {
                    return NotFound(new { success = false, message = "Không có dữ liệu để xuất." });
                }

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("DPUCurrent");
                    int row = 1;

                    // Định nghĩa các cột cố định
                    var staticColumns = new List<string>
            {
                "SerialNumber", "ModelName", "TestCode", "Data1", "WIPGroup", "TestTime"
            };

                    // Ghi tiêu đề các cột
                    int col = 1;
                    foreach (var column in staticColumns)
                    {
                        worksheet.Cells[row, col++].Value = column;
                    }

                    // Lấy các cột động (ngoài cột cố định)
                    var dynamicColumns = typeof(DPUCurrent)
                        .GetProperties()
                        .Select(p => p.Name)
                        .Where(p => !staticColumns.Contains(p))
                        .ToList();

                    foreach (var column in dynamicColumns)
                    {
                        worksheet.Cells[row, col++].Value = column;
                    }

                    // Duyệt qua dữ liệu và ghi vào Excel
                    foreach (var serialGroup in data.GroupBy(x => x.SerialNumber))
                    {
                        var baseRow = ++row; // Ghi vào dòng mới

                        // Ghi các cột tĩnh
                        var firstRecord = serialGroup.First();
                        col = 1;
                        worksheet.Cells[row, col++].Value = firstRecord.SerialNumber;
                        worksheet.Cells[row, col++].Value = firstRecord.ModelName;
                        worksheet.Cells[row, col++].Value = firstRecord.TestCode;
                        worksheet.Cells[row, col++].Value = firstRecord.Data1;
                        worksheet.Cells[row, col++].Value = firstRecord.WIPGroup;
                        worksheet.Cells[row, col++].Value = firstRecord.TestTime;

                        // Ghi các cột động (từng bản ghi thành cột)
                        col = staticColumns.Count + 1;
                        foreach (var record in serialGroup)
                        {
                            foreach (var dynamicColumn in dynamicColumns)
                            {
                                var value = typeof(DPUCurrent).GetProperty(dynamicColumn)?.GetValue(record, null);
                                worksheet.Cells[baseRow, col++].Value = value?.ToString();
                            }
                        }
                    }

                    // Tự động điều chỉnh kích thước cột
                    worksheet.Cells.AutoFitColumns();

                    // Xuất file Excel
                    var fileName = $"DPUCurrent_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
                    var fileBytes = package.GetAsByteArray();
                    return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi khi xuất dữ liệu: {ex.Message}" });
            }
        }
    }
    public class ProcessSNRequest
    {
        public string SerialNumber { get; set; }
        public string Status { get; set; } // PASS/FAIL.
        public string DDRToolFailCode { get; set; } // Giá trị FAIL cho DDR-TOOL.
        public string HASSFailCode { get; set; } // Giá trị FAIL cho HASS.
        public string FT_OFFFailCode { get; set; } // Giá trị FAIL cho FT-OFF.
        public string FT_HASSFailCode { get; set; } // Giá trị FAIL cho FT-HASS.
        public string NextStation { get; set; } // Trạm tiếp theo cho CUSTOMER.
        public string CustomerInput { get; set; } // Giá trị nhập cho cột Customer.
        public string RERepairInput { get; set; } //Giá trị nhập cho cột RERepair.
        public string Owner { get; set; } //.
    }
    public class UpdateValueRequest
    {
        public string SerialNumber { get; set; }
        public int ColumnIndex { get; set; }
        public string NewValue { get; set; }
    }
    public class UpdateRequest
    {
        public string SerialNumber { get; set; }
        public string PEInstruction { get; set; }
        public string NVInstruction { get; set; }
        public string RERepair { get; set; }
    }

}
