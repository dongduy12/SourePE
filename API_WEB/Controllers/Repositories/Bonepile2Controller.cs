using API_WEB.Models.Bonepile;
using API_WEB.Models.Repositories;
using API_WEB.ModelsDB;
using API_WEB.ModelsOracle;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using Oracle.ManagedDataAccess.Client;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Collections.Generic;

namespace API_WEB.Controllers.Repositories
{
    [Route("api/[controller]")]
    [ApiController]
    public class Bonepile2Controller : ControllerBase
    {

        private readonly CSDL_NE _sqlContext;
        private readonly OracleDbContext _oracleContext;

        public Bonepile2Controller(CSDL_NE sqlContext, OracleDbContext oracleContext)
        {
            _sqlContext = sqlContext;
            _oracleContext = oracleContext;
        }

        /// <summary>
        /// Bonepile 2.0
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("data")]
        public async Task<IActionResult> DetailStatus([FromBody] StatusRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { message = "Yeu cau khong hop le!" });
                }
                //var statuses = request.Statuses?.Any() == true ? request.Statuses : new List<string> { "Repair", "CheckOut", "CheckIn", "WaitingLink", "Online", "WaitingApproveScrap", "Scrap" };
                var statuses = request.Statuses?.Any() == true ? request.Statuses : new List<string> { "Repair", "CheckOut", "CheckIn", "WaitingLink", "Online", "WaitingKanBanIn", "WaitingApproveScrap", "Scrap" };
                //Kiem tra dinh dang ngay
                if (string.IsNullOrEmpty(request.StartDate) || string.IsNullOrEmpty(request.EndDate))
                {
                    return BadRequest(new { message = "StartDate va EndDate khong duoc de trong!" });
                }
                if (!DateTime.TryParseExact(request.StartDate, "yyyy/MM/dd HH:mm", null, System.Globalization.DateTimeStyles.None, out _)
                    || !DateTime.TryParseExact(request.EndDate, "yyyy/MM/dd HH:mm", null, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(new { message = "??nh d?ng StartDate ho?c EndDate khong h?p l?. Vui long s? d?ng ??nh d?ng yyyy/MM/dd HH:mm." });
                }

                //THuc thien truy van Oracle
                var bonepileData = await ExecuteOracleQuery(request);

                if (!bonepileData.Any())
                {
                    return NotFound(new { message = "Khong tim thay du lieu!!", count = 0 });
                }


                var scrapCategories = await _sqlContext.ScrapLists
                    .Where(s => bonepileData.Select(b => b.SN).Contains(s.SN))
                    .Select(s => new ScrapListCategory { SN = s.SN, Category = s.Category })
                    .ToListAsync();

                //var validStatuses = new HashSet<string> { "Repair", "CheckOut", "CheckIn", "WaitingLink", "Online", "WaitingApproveScrap", "Scrap" };
                var validStatuses = new HashSet<string> { "Repair", "CheckOut", "CheckIn", "WaitingLink", "Online", "WaitingKanBanIn", "WaitingApproveScrap", "Scrap" };
                var result = bonepileData.Select(b =>
                {
                    var scrapCategory = scrapCategories.FirstOrDefault(c => c.SN == b.SN);
                    string status;
                    if (scrapCategory != null)
                    {
                        status = scrapCategory.Category == "Scrap" ? "Scrap" : "WaitingApproveScrap";
                    }
                    else if (b.WIP_GROUP.Contains("B31M"))
                    {
                        status = "WaitingLink";
                    }
                    else
                    {
                        // Khong co d? li?u trong ScrapLists, dung ERROR_FLAG
                        status = b.ERROR_FLAG switch
                        {
                            "7" => "Repair",
                            "8" => "CheckOut",
                            "1" => "CheckIn",
                            "0" => b.WIP_GROUP.Contains("KANBAN_IN") ? "WaitingKanBanIn" : "Online",
                            _ => "Unknown" // X? ly cac gia tr? ERROR_FLAG khong xac ??nh
                        };
                    }
                    return new
                    {
                        SN = b.SN,
                        MoNumber = b.MO_NUMBER,
                        ModelName = b.MODEL_NAME,
                        ProductLine = b.PRODUCT_LINE,
                        FailStation = b.FAIL_STATION,
                        Symptom = b.SYMPTOM,
                        ErrorDescription = b.ERROR_DESC,
                        Time = b.TIME.ToString("yyyy-MM-dd HH:mm:ss"),
                        Flag = b.FLAG,
                        PONo = b.PO_NO,
                        POItem = b.PO_ITEM,
                        FailureAging = b.FAILURE_AGING,
                        WipGroup = b.WIP_GROUP,
                        VersionCode = b.VERSION_CODE,
                        WorkFlag = b.WORK_FLAG,
                        ErrorFlag = b.ERROR_FLAG,
                        MONew = b.MO_NEW,
                        Status = status,
                        CheckinRepairTime = b.CHECKIN_REPAIR_TIME?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A",
                        CheckoutRepairTime = b.CHECKOUT_REPAIR_TIME?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A",
                        ScrapStatus = b.SCRAP_STATUS,
                        Category = scrapCategory?.Category ?? "N/A"
                    };
                }).Where(r => validStatuses.Contains(r.Status) && request.Statuses.Contains(r.Status)).ToList();
                if (!result.Any())
                {
                    return NotFound(new { message = "Khong tim thay du lieu!!", count = 0 });
                }
                return Ok(new
                {
                    count = result.Count,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Xay ra loi", error = ex.Message });
            }
        }

        // API m?i: StatusCount
        [HttpPost("status-count")]
        public async Task<IActionResult> StatusCount([FromBody] StatusRequest request)
        {
            try
            {
                // Ki?m tra ??u vao
                if (request == null)
                {
                    return BadRequest(new { message = "Yeu cau khong hop le!" });
                }

                // N?u khong co Statuses, s? d?ng t?t c? tr?ng thai h?p l?
                //var statuses = request.Statuses?.Any() == true ? request.Statuses : new List<string> { "Repair", "CheckOut", "CheckIn", "WaitingLink", "Online", "WaitingApproveScrap", "Scrap" };
                var statuses = request.Statuses?.Any() == true ? request.Statuses : new List<string> { "Repair", "CheckOut", "CheckIn", "WaitingLink", "Online", "WaitingKanBanIn", "WaitingApproveScrap", "Scrap" };

                if (string.IsNullOrEmpty(request.StartDate) || string.IsNullOrEmpty(request.EndDate))
                {
                    return BadRequest(new { message = "StartDate va EndDate khong duoc de trong!" });
                }
                if (!DateTime.TryParseExact(request.StartDate, "yyyy/MM/dd HH:mm", null, System.Globalization.DateTimeStyles.None, out _)
                    || !DateTime.TryParseExact(request.EndDate, "yyyy/MM/dd HH:mm", null, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(new { message = "??nh d?ng StartDate ho?c EndDate khong h?p l?. Vui long s? d?ng ??nh d?ng yyyy/MM/dd HH:mm." });
                }

                // Th?c hi?n truy v?n Oracle
                var bonepileData = await ExecuteOracleQuery(request);

                if (!bonepileData.Any())
                {
                    return NotFound(new { message = "Khong tim thay du lieu!!", count = 0, statusCounts = new { } });
                }

                // L?y Category t? ScrapLists (SQL Server)
                var scrapCategories = await _sqlContext.ScrapLists
                    .Where(s => bonepileData.Select(b => b.SN).Contains(s.SN))
                    .Select(s => new ScrapListCategory { SN = s.SN, Category = s.Category })
                    .ToListAsync();

                // Xac ??nh tr?ng thai va ??m s? l??ng
                //var validStatuses = new HashSet<string> { "Repair", "CheckOut", "CheckIn", "WaitingLink", "Online", "WaitingApproveScrap", "Scrap" };
                var validStatuses = new HashSet<string> { "Repair", "CheckOut", "CheckIn", "WaitingLink", "Online", "WaitingKanBanIn", "WaitingApproveScrap", "Scrap" };
                var statusCounts = new Dictionary<string, int>
                {
                    { "Repair", 0 },
                    { "CheckOut", 0 },
                    { "CheckIn", 0 },
                    { "WaitingLink", 0 },
                    { "Online", 0 },
                    { "WaitingKanBanIn", 0 },
                    { "WaitingApproveScrap", 0 },
                    { "Scrap", 0 }
                };

                foreach (var b in bonepileData)
                {
                    var scrapCategory = scrapCategories.FirstOrDefault(c => c.SN == b.SN);
                    string status;
                    if (scrapCategory != null)
                    {
                        status = scrapCategory.Category == "Scrap" ? "Scrap" : "WaitingApproveScrap";
                    }else if (b.WIP_GROUP.Contains("B31M"))
                    {
                        status = "WaitingLink";
                    }
                    else
                    {
                        status = b.ERROR_FLAG switch
                        {
                            "7" => "Repair",
                            "8" => "CheckOut",
                            "1" => "CheckIn",
                            "0" => b.WIP_GROUP.Contains("KANBAN_IN") ? "WaitingKanBanIn" : "Online",
                            //"0" => "Online",
                            _ => "Repair"
                        };
                    }

                    if (validStatuses.Contains(status) && request.Statuses.Contains(status))
                    {
                        statusCounts[status]++;
                    }
                }

                // T?ng s? b?n ghi kh?p
                int totalCount = statusCounts.Values.Sum();

                if (totalCount == 0)
                {
                    return NotFound(new { message = "Khong tim thay du lieu!!", count = 0, statusCounts = new { } });
                }

                // Lo?i b? cac tr?ng thai co s? l??ng 0 ?? tr? v? JSON g?n h?n
                var filteredStatusCounts = statusCounts
                    .Where(kv => kv.Value > 0)
                    .ToDictionary(kv => kv.Key, kv => kv.Value);

                return Ok(new
                {
                    count = totalCount,
                    statusCounts = filteredStatusCounts
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Xay ra loi", error = ex.Message });
            }
        }
        private async Task<List<BonepileResult>> ExecuteOracleQuery(StatusRequest request)
        {
            var result = new List<BonepileResult>();

            await using var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
            await connection.OpenAsync();

            string query = @"
                SELECT 
                    c.SERIAL_NUMBER, 
                    c.MODEL_NAME, 
                    c.PRODUCT_LINE, 
                    c.MO_NUMBER, 
                    c.FAIL_STATION, 
                    c.SYMPTOM, 
                    c.ERROR_DESC, 
                    c.TIME, 
                    c.FLAG, 
                    c.PO_NO, 
                    c.PO_ITEM, 
                    c.FAILURE_AGING, 
                    c.WIP_GROUP, 
                    c.VERSION_CODE, 
                    c.WORK_FLAG,
                    CASE 
                        WHEN c.ERROR_FLAG = '0' AND c.MO_NEW IS NOT NULL THEN '0'
                        WHEN c.ERROR_FLAG = '0' AND c.MO_NEW IS NULL THEN '2'
                        ELSE c.ERROR_FLAG 
                    END AS ERROR_FLAG,
                    c.MO_NEW, 
                    c.STATUS, 
                    c.CHECKIN_REPAIR_TIME, 
                    c.CHECKOUT_REPAIR_TIME,
                    CASE 
                        WHEN s.SERIAL_NUMBER IS NOT NULL THEN 'SCRAP' 
                        ELSE '' 
                    END AS SCRAP_STATUS
                FROM (
                    SELECT 
                        a.*, 
                        b.MO_NUMBER1, 
                        b.DATA1, 
                        b.ERROR_FLAG, 
                        b.WORK_FLAG, 
                        b.WIP_GROUP, 
                        b.VERSION_CODE,
                        CASE 
                            WHEN a.MO_NUMBER <> b.MO_NUMBER1 THEN b.MO_NUMBER1
                            WHEN a.MO_NUMBER = b.MO_NUMBER1 
                                AND b.ERROR_FLAG NOT IN ('1', '7', '8') 
                                AND b.DATA1 IS NOT NULL THEN b.DATA1
                            ELSE '' 
                        END AS MO_NEW,
                        CASE 
                            WHEN a.FAILURE_AGING <= 7 THEN 'Normal <7 DAY'
                            WHEN a.FAILURE_AGING > 7 AND a.FAILURE_AGING < 14 THEN 'Medium >7,<14 DAY'
                            ELSE 'High >14 DAY'
                        END AS STATUS,
                        r.IN_DATETIME AS CHECKIN_REPAIR_TIME,
                        r.OUT_DATETIME AS CHECKOUT_REPAIR_TIME
                    FROM (
                        SELECT 
                            a.SERIAL_NUMBER, 
                            a.MODEL_NAME, 
                            a.PRODUCT_LINE, 
                            a.MO_NUMBER, 
                            a.TEST_STATION AS FAIL_STATION, 
                            a.SYMPTOM, 
                            b.ERROR_DESC, 
                            a.TIME, 
                            a.FLAG, 
                            a.PO_NO, 
                            a.PO_ITEM, 
                            ROUND((SYSDATE - a.TIME), 2) AS FAILURE_AGING
                        FROM SFISM4.NVIDIA_BONPILE_SN_LOG a
                        INNER JOIN SFIS1.C_ERROR_CODE_T b 
                            ON a.SYMPTOM = b.ERROR_CODE
                        WHERE a.TIME BETWEEN TO_DATE(:start_date, 'YYYY/MM/DD HH24:MI') 
                            AND TO_DATE(:end_date, 'YYYY/MM/DD HH24:MI')
                            AND NOT EXISTS (
                                SELECT 1 
                                FROM SFISM4.Z_KANBAN_TRACKING_T z 
                                WHERE z.SERIAL_NUMBER = a.SERIAL_NUMBER
                            )
                            AND NOT EXISTS (
                                SELECT 1 
                                FROM SFISM4.R_NV_REPAIR_BONEPILE_T r 
                                WHERE r.SERIAL_NUMBER = a.SERIAL_NUMBER 
                                AND r.DATA7 = 'SCRAP'
                            )
                            AND a.PRODUCT_LINE NOT LIKE 'SA%'
                            AND a.PRODUCT_LINE NOT LIKE 'Switch%'
                            AND a.MODEL_NAME NOT LIKE 'SA%'
                    ) a
                    LEFT JOIN (
                        SELECT 
                            a.SERIAL_NUMBER, 
                            a.MO_NUMBER AS MO_NUMBER1, 
                            b.DATA1, 
                            a.ERROR_FLAG, 
                            a.WORK_FLAG, 
                            a.WIP_GROUP, 
                            a.VERSION_CODE
                        FROM SFISM4.R107 a
                        LEFT JOIN SFISM4.R_KEYPART_BLACK_WHITE_LIST_T b 
                            ON a.SERIAL_NUMBER = b.KEY_PART_SN 
                            AND b.TYPE = 'LINK_MO'
                    ) b 
                        ON a.SERIAL_NUMBER = b.SERIAL_NUMBER
                    LEFT JOIN SFISM4.R_REPAIR_IN_OUT_T r 
                        ON a.SERIAL_NUMBER = r.SERIAL_NUMBER 
                        AND a.FAIL_STATION = r.STATION_NAME 
                        AND a.MO_NUMBER = r.MO_NUMBER
                ) c
                LEFT JOIN (
                    SELECT SERIAL_NUMBER, TEST_GROUP, TEST_CODE
                    FROM SFISM4.R109 
                    WHERE REASON_CODE = 'B001'
                ) s 
                    ON c.SERIAL_NUMBER = s.SERIAL_NUMBER 
                    AND c.FAIL_STATION = s.TEST_GROUP 
                    AND c.SYMPTOM = s.TEST_CODE
                ORDER BY c.TIME";

            using (var command = new OracleCommand(query, connection))
            {
                // Bind parameters
                command.Parameters.Add("start_date", OracleDbType.Varchar2).Value = request.StartDate;
                command.Parameters.Add("end_date", OracleDbType.Varchar2).Value = request.EndDate;

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.Add(new BonepileResult
                        {
                            SN = reader["SERIAL_NUMBER"].ToString(),
                            MODEL_NAME = reader["MODEL_NAME"].ToString(),
                            PRODUCT_LINE = reader["PRODUCT_LINE"].ToString(),
                            MO_NUMBER = reader["MO_NUMBER"].ToString(),
                            FAIL_STATION = reader["FAIL_STATION"].ToString(),
                            SYMPTOM = reader["SYMPTOM"].ToString(),
                            ERROR_DESC = reader["ERROR_DESC"].ToString(),
                            TIME = Convert.ToDateTime(reader["TIME"]),
                            FLAG = reader["FLAG"].ToString(),
                            PO_NO = reader["PO_NO"].ToString(),
                            PO_ITEM = reader["PO_ITEM"].ToString(),
                            FAILURE_AGING = Convert.ToDouble(reader["FAILURE_AGING"]),
                            WIP_GROUP = reader["WIP_GROUP"].ToString(),
                            VERSION_CODE = reader["VERSION_CODE"].ToString(),
                            WORK_FLAG = reader["WORK_FLAG"].ToString(),
                            ERROR_FLAG = reader["ERROR_FLAG"].ToString(),
                            MO_NEW = reader["MO_NEW"].ToString(),
                            STATUS = reader["STATUS"].ToString(),
                            CHECKIN_REPAIR_TIME = reader["CHECKIN_REPAIR_TIME"] != DBNull.Value ? Convert.ToDateTime(reader["CHECKIN_REPAIR_TIME"]) : (DateTime?)null,
                            CHECKOUT_REPAIR_TIME = reader["CHECKOUT_REPAIR_TIME"] != DBNull.Value ? Convert.ToDateTime(reader["CHECKOUT_REPAIR_TIME"]) : (DateTime?)null,
                            SCRAP_STATUS = reader["SCRAP_STATUS"].ToString()
                        });
                    }
                }
            }
            return result;
        }


        /// <summary>
        /// Bonpile Before
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>

        [HttpPost("adapter-repair-records")]
        public async Task<IActionResult> AdapterRepairRecords([FromBody] StatusRequestBonepile request)
        {
            try
            {
                if (request == null) return BadRequest(new { message = "Yêu cầu không hợp lệ!" });

                bool filterByStatus = request.Statuses?.Any() == true;
                var statuses = filterByStatus ? request.Statuses.Where(s => !string.IsNullOrEmpty(s)).ToList() : null;

                // 1) Lấy dữ liệu 2 nguồn
                var baseData = await ExecuteAdapterRepairQuery();
                var b28mData = await ExecuteAdapterReworkFgQuery();

                // 2) Set dùng để ép trạng thái Rework FG
                var reworkFgSet = new HashSet<string>(
                    b28mData.Select(x => x.SERIAL_NUMBER?.Trim().ToUpper() ?? ""),
                    StringComparer.OrdinalIgnoreCase
                );

                // 3) Hợp nhất theo SN để tránh trùng (ưu tiên bản ghi đầu – bạn có thể đổi thành chọn mới nhất)
                var allData = baseData.Concat(b28mData)
                    .GroupBy(x => (x.SERIAL_NUMBER ?? "").Trim().ToUpper())
                    .Select(g => g.First())
                    .ToList();

                // Scrap dict
                var scrapDict = (await _sqlContext.ScrapLists
                    .Select(s => new { SN = s.SN, s.ApplyTaskStatus, s.TaskNumber })
                    .ToListAsync())
                    .ToDictionary(
                        c => c.SN?.Trim().ToUpper() ?? "",
                        c => (c.ApplyTaskStatus, c.TaskNumber),
                        StringComparer.OrdinalIgnoreCase
                    );

                var validStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Scrap Lacks Task","Scrap Has Scrap","ApprovedBGA","Waiting approve BGA",
            "Waiting approve scrap","Rework FG","Under repair in RE","Waiting Check Out","Under repair in PD"
        };

                var result = allData.Select(b =>
                {
                    var sn = b.SERIAL_NUMBER?.Trim().ToUpper() ?? "";
                    string status;

                    // ƯU TIÊN: Thuộc B28M => luôn Rework FG
                    if (reworkFgSet.Contains(sn))
                    {
                        status = "Rework FG";
                    }
                    else if (scrapDict.TryGetValue(sn, out var scrapInfo))
                    {
                        var applyTaskStatus = scrapInfo.ApplyTaskStatus;
                        var taskNumber = scrapInfo.TaskNumber;

                        if (applyTaskStatus == 0 || applyTaskStatus == 1 || applyTaskStatus == 5 || applyTaskStatus == 6 || applyTaskStatus == 7)
                            status = string.IsNullOrEmpty(taskNumber) ? "Scrap Lacks Task" : "Scrap Has Scrap";
                        else
                            status = applyTaskStatus switch
                            {
                                2 => "Waiting approve scrap",
                                3 => "ApprovedBGA",
                                4 => "Waiting approve BGA",
                                _ => "Under repair in PD"
                            };
                    }
                    else if (b.MO_NUMBER?.Trim().StartsWith("4") == true)
                    {
                        // vẫn giữ luật cũ cho case ngoài B28M
                        status = "Rework FG";
                    }
                    else
                    {
                        status = b.ERROR_FLAG switch
                        {
                            "7" => "Under repair in RE",
                            "8" => "Waiting Check Out",
                            _ => "Under repair in PD"
                        };
                    }

                    return new
                    {
                        SN = b.SERIAL_NUMBER,
                        ModelName = b.MODEL_NAME,
                        MoNumber = b.MO_NUMBER,
                        ProductLine = b.PRODUCT_LINE,
                        ErrorFlag = b.ERROR_FLAG,
                        WorkFlag = b.WORK_FLAG,
                        WipGroup = b.WIP_GROUP,
                        Data11 = b.DATA11,
                        Status = status,
                        testTime = b.TEST_TIME,
                        testCode = b.TEST_CODE,
                        testGroup = b.TEST_GROUP,
                        errorDesc = b.ERROR_DESC,
                        repair = b.REPAIR,
                        agingDay = b.AGING_DAY,
                        checkInDate = b.CHECKIN_DATE
                    };
                })
                .Where(r => validStatuses.Contains(r.Status, StringComparer.OrdinalIgnoreCase)
                         && (!filterByStatus || statuses.Contains(r.Status, StringComparer.OrdinalIgnoreCase)))
                .ToList();

                if (!result.Any()) return NotFound(new { message = "Không tìm thấy dữ liệu!", count = 0 });

                return Ok(new { count = result.Count, data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Xảy ra lỗi", error = ex.Message });
            }
        }

        [HttpGet("adapter-repair-status-count")]
        public async Task<IActionResult> AdapterRepairStatusCount()
        {
            try
            {
                var baseData = await ExecuteAdapterRepairQuery();
                var b28mData = await ExecuteAdapterReworkFgQuery();

                var reworkFgSet = new HashSet<string>(
                    b28mData.Select(x => x.SERIAL_NUMBER?.Trim().ToUpper() ?? ""),
                    StringComparer.OrdinalIgnoreCase
                );

                var allData = baseData.Concat(b28mData)
                    .GroupBy(x => (x.SERIAL_NUMBER ?? "").Trim().ToUpper())
                    .Select(g => g.First())
                    .ToList();

                var scrapDict = (await _sqlContext.ScrapLists
                    .Select(s => new { s.SN, s.ApplyTaskStatus, s.TaskNumber })
                    .ToListAsync())
                    .ToDictionary(
                        c => c.SN?.Trim().ToUpper() ?? "",
                        c => (c.ApplyTaskStatus, c.TaskNumber),
                        StringComparer.OrdinalIgnoreCase
                    );

                var result = allData.Select(b =>
                {
                    var sn = b.SERIAL_NUMBER?.Trim().ToUpper() ?? "";
                    if (reworkFgSet.Contains(sn)) return "Rework FG";

                    if (scrapDict.TryGetValue(sn, out var scrapInfo))
                    {
                        var applyTaskStatus = scrapInfo.ApplyTaskStatus;
                        var taskNumber = scrapInfo.TaskNumber;

                        if (applyTaskStatus == 0 || applyTaskStatus == 1 || applyTaskStatus == 5 || applyTaskStatus == 6 || applyTaskStatus == 7)
                            return string.IsNullOrEmpty(taskNumber) ? "Scrap Lacks Task" : "Scrap Has Scrap";

                        return applyTaskStatus switch
                        {
                            2 => "Waiting approve scrap",
                            3 => "ApprovedBGA",
                            4 => "Waiting approve BGA",
                            _ => "Under repair in PD"
                        };
                    }

                    if (b.MO_NUMBER?.Trim().StartsWith("4") == true) return "Rework FG";

                    return b.ERROR_FLAG switch
                    {
                        "7" => "Under repair in RE",
                        "8" => "Waiting Check Out",
                        _ => "Under repair in PD"
                    };
                })
                .ToList();

                var statusCounts = result
                    .GroupBy(s => s)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToList();

                return Ok(new { totalCount = result.Count, statusCounts });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Xảy ra lỗi", error = ex.Message });
            }
        }


        [HttpGet("adapter-repair-aging-count")]
        public async Task<IActionResult> AdapterRepairAgingCount()
        {
            try
            {
                var baseData = await ExecuteAdapterRepairQuery();
                var b28mData = await ExecuteAdapterReworkFgQuery();

                var reworkFgSet = new HashSet<string>(
                    b28mData.Select(x => x.SERIAL_NUMBER?.Trim().ToUpper() ?? ""),
                    StringComparer.OrdinalIgnoreCase
                );

                var allData = baseData.Concat(b28mData)
                    .GroupBy(x => (x.SERIAL_NUMBER ?? "").Trim().ToUpper())
                    .Select(g => g.First())
                    .ToList();

                var scrapDict = (await _sqlContext.ScrapLists
                    .Select(s => new { s.SN, s.ApplyTaskStatus, s.TaskNumber })
                    .ToListAsync())
                    .ToDictionary(
                        c => c.SN?.Trim().ToUpper() ?? "",
                        c => (c.ApplyTaskStatus, c.TaskNumber),
                        StringComparer.OrdinalIgnoreCase
                    );

                var validStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Scrap Lacks Task","Scrap Has Scrap","ApprovedBGA","Waiting approve BGA",
            "Waiting approve scrap","Rework FG","Under repair in RE","Waiting Check Out","Under repair in PD"
        };

                var records = allData.Select(b =>
                {
                    var sn = b.SERIAL_NUMBER?.Trim().ToUpper() ?? "";
                    string status;

                    if (reworkFgSet.Contains(sn))
                    {
                        status = "Rework FG";
                    }
                    else if (scrapDict.TryGetValue(sn, out var scrapInfo))
                    {
                        var applyTaskStatus = scrapInfo.ApplyTaskStatus;
                        var taskNumber = scrapInfo.TaskNumber;

                        if (applyTaskStatus == 0 || applyTaskStatus == 1 || applyTaskStatus == 5 || applyTaskStatus == 6 || applyTaskStatus == 7)
                            status = string.IsNullOrEmpty(taskNumber) ? "Scrap Lacks Task" : "Scrap Has Scrap";
                        else
                            status = applyTaskStatus switch
                            {
                                2 => "Waiting approve scrap",
                                3 => "ApprovedBGA",
                                4 => "Waiting approve BGA",
                                _ => "Under repair in PD"
                            };
                    }
                    else if (b.MO_NUMBER?.Trim().StartsWith("4") == true)
                    {
                        status = "Rework FG";
                    }
                    else
                    {
                        status = b.ERROR_FLAG switch
                        {
                            "7" => "Under repair in RE",
                            "8" => "Waiting Check Out",
                            _ => "Under repair in PD"
                        };
                    }

                    return new
                    {
                        SN = b.SERIAL_NUMBER,
                        ModelName = b.MODEL_NAME,
                        MoNumber = b.MO_NUMBER,
                        ProductLine = b.PRODUCT_LINE,
                        ErrorFlag = b.ERROR_FLAG,
                        WorkFlag = b.WORK_FLAG,
                        WipGroup = b.WIP_GROUP,
                        Data11 = b.DATA11,
                        Status = status,
                        testTime = b.TEST_TIME,
                        testCode = b.TEST_CODE,
                        testGroup = b.TEST_GROUP,
                        errorDesc = b.ERROR_DESC,
                        repair = b.REPAIR,
                        agingDay = b.AGING_DAY,
                        checkInDate = b.CHECKIN_DATE
                    };
                })
                .Where(r => validStatuses.Contains(r.Status, StringComparer.OrdinalIgnoreCase))
                .ToList();

                var agingGroups = records
                    .GroupBy(r =>
                    {
                        if (double.TryParse(r.agingDay, out double aging))
                        {
                            if (aging < 30) return "<30";
                            if (aging <= 90) return "30-90";
                            return ">90";
                        }
                        return ">90";
                    })
                    .Select(g => new { AgeRange = g.Key, Count = g.Count(), Records = g.ToList() })
                    .ToList();

                return Ok(new { totalCount = records.Count, agingCounts = agingGroups });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Xảy ra lỗi", error = ex.Message });
            }
        }

        private async Task<List<RepairTaskResult>> ExecuteAdapterRepairQuery()
        {
            var result = new List<RepairTaskResult>();

            await using var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
            await connection.OpenAsync();

            string query = @"
                    SELECT 
                        r107.SERIAL_NUMBER,
                        r107.MODEL_NAME,
                        model_desc.PRODUCT_LINE,
                        r107.MO_NUMBER,
                        r107.ERROR_FLAG,
                        r107.WORK_FLAG,
                        r107.WIP_GROUP,
                        r109_latest.TEST_GROUP,
                        r109_latest.TEST_TIME,
                        r109_latest.TEST_CODE,
                        error_desc.ERROR_DESC,
                        repair_task.DATA11,

                        -- G?p DATA19 theo t?ng SERIAL_NUMBER (ch? l?y cac dong DATA17 = Confirm/Save (khong phan bi?t hoa th??ng))
                        rt19.DATA19_COMBINED,

                        /* m?c CHECK_IN ??u tien */
                        chkin.first_checkin_date AS CHECKIN_DATE,
                        /* Aging theo ngay (s? ngay k? t? CHECK_IN ??u tien t?i hi?n t?i) */
                        TRUNC(SYSDATE - chkin.first_checkin_date) AS AGING_DAY
                    FROM sfism4.r107 r107
                    JOIN sfis1.c_model_desc_t model_desc
                      ON r107.model_name = model_desc.model_name

                    LEFT JOIN sfism4.r_repair_task_t repair_task
                      ON r107.SERIAL_NUMBER = repair_task.SERIAL_NUMBER

                    /* L?y CHECK_IN ??u tien (DATE3 nh? nh?t) cho m?i SERIAL_NUMBER */
                    LEFT JOIN (
                        SELECT
                            d.SERIAL_NUMBER,
                            MIN(d.DATE3) AS first_checkin_date
                        FROM sfism4.R_REPAIR_TASK_DETAIL_T d
                        WHERE d.DATA12 = 'CHECK_IN'
                        GROUP BY d.SERIAL_NUMBER
                    ) chkin
                      ON chkin.SERIAL_NUMBER = r107.SERIAL_NUMBER

                    /* G?p DATA19 cho m?i SERIAL_NUMBER, ch? l?y khi DATA17 la Confirm/Save (khong phan bi?t hoa th??ng) */
                    LEFT JOIN (
                        SELECT
                            d.SERIAL_NUMBER,
                            LISTAGG(TRIM(d.DATA19), ' | ') WITHIN GROUP (ORDER BY d.DATE3) AS DATA19_COMBINED
                        FROM sfism4.R_REPAIR_TASK_DETAIL_T d
                        WHERE UPPER(d.DATA17) IN ('CONFIRM', 'SAVE')
                          AND d.DATA19 IS NOT NULL
                        GROUP BY d.SERIAL_NUMBER
                    ) rt19
                      ON rt19.SERIAL_NUMBER = r107.SERIAL_NUMBER

                    /* L?y b?n ghi TEST_CODE m?i nh?t t? R109 theo t?ng SERIAL_NUMBER */
                    LEFT JOIN (
                        SELECT SERIAL_NUMBER, TEST_CODE, TEST_TIME, TEST_GROUP
                        FROM (
                            SELECT 
                                R109.SERIAL_NUMBER,
                                R109.TEST_CODE,
                                R109.TEST_TIME,
                                R109.TEST_GROUP,
                                ROW_NUMBER() OVER(
                                    PARTITION BY R109.SERIAL_NUMBER
                                    ORDER BY R109.TEST_TIME DESC, R109.TEST_CODE DESC
                                ) AS rn
                            FROM SFISM4.R109 R109
                        ) t
                        WHERE t.rn = 1
                    ) r109_latest
                      ON r109_latest.SERIAL_NUMBER = r107.SERIAL_NUMBER

                    INNER JOIN sfis1.C_ERROR_CODE_T error_desc
                      ON r109_latest.TEST_CODE = error_desc.ERROR_CODE

                    WHERE 
                          model_desc.MODEL_SERIAL = 'ADAPTER'
                      AND r107.SERIAL_NUMBER NOT IN (
                          SELECT SERIAL_NUMBER FROM SFISM4.Z_KANBAN_TRACKING_T
                      )
                      AND r107.MODEL_NAME NOT LIKE '900%'
                      AND r107.MODEL_NAME NOT LIKE '692%'
                      AND r107.MODEL_NAME NOT LIKE 'TB%'
                      AND r107.WIP_GROUP NOT LIKE '%BR2C%'
                      AND r107.WIP_GROUP NOT LIKE '%BCFA%'
                      AND (
                            /* Nhom c?: ERROR_FLAG 7 ho?c 8 */
                            r107.ERROR_FLAG IN ('7','8')
                            /* Nhom b? sung: ERROR_FLAG = 1 va test m?i nh?t ngoai 8 gi? g?n nh?t */
                            OR ( r107.ERROR_FLAG = '1'
                                 AND r109_latest.TEST_TIME <= SYSDATE - (8/24)
                               )
                          )
                      AND r109_latest.TEST_CODE NOT IN (
                        'BV00','PP10','BRK00','HSK00','SCR00','C028','TA00','CAR0','C010','C012',
                        'LBxx','GB00','CLExx','GL00','BHSK00','DIR02','DIR03','DR00','GF06',
                        'CLE02','CLE03','CLE04','CLE05','CLE06','CLE07','CLE08','LB01','LB02',
                        'LB03','LB04','LB05','LB06','LB07','CK00'
                      )
                    ";

            using (var command = new OracleCommand(query, connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.Add(new RepairTaskResult
                        {
                            SERIAL_NUMBER = reader["SERIAL_NUMBER"].ToString(),
                            MODEL_NAME = reader["MODEL_NAME"].ToString(),
                            PRODUCT_LINE = reader["PRODUCT_LINE"].ToString(),
                            MO_NUMBER = reader["MO_NUMBER"].ToString(),
                            ERROR_FLAG = reader["ERROR_FLAG"] != DBNull.Value ? reader["ERROR_FLAG"].ToString() : null,
                            WORK_FLAG = reader["WORK_FLAG"] != DBNull.Value ? reader["WORK_FLAG"].ToString() : null,
                            WIP_GROUP = reader["WIP_GROUP"] != DBNull.Value ? reader["WIP_GROUP"].ToString() : null,
                            TEST_GROUP = reader["TEST_GROUP"] != DBNull.Value ? reader["TEST_GROUP"].ToString() : null,
                            TEST_TIME = reader["TEST_TIME"].ToString(),
                            TEST_CODE = reader["TEST_CODE"].ToString(),
                            ERROR_DESC = reader["ERROR_DESC"].ToString(),
                            DATA11 = reader["DATA11"] != DBNull.Value ? reader["DATA11"].ToString() : null,
                            REPAIR = reader["DATA19_COMBINED"] != DBNull.Value ? reader["DATA19_COMBINED"].ToString() : null,
                            CHECKIN_DATE = reader["CHECKIN_DATE"] != DBNull.Value ? Convert.ToDateTime(reader["CHECKIN_DATE"]) : (DateTime?)null,
                            AGING_DAY = reader["AGING_DAY"] != DBNull.Value ? reader["AGING_DAY"].ToString() : null,
                        });
                    }
                }
            }

            return result;
        }

        private async Task<List<RepairTaskResult>> ExecuteAdapterReworkFgQuery()
        {
            var result = new List<RepairTaskResult>();

            await using var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
            await connection.OpenAsync();

            string query = @"
        SELECT 
            r107.SERIAL_NUMBER,
            r107.MODEL_NAME,
            model_desc.PRODUCT_LINE,
            r107.MO_NUMBER,
            r107.ERROR_FLAG,
            r107.WORK_FLAG,
            r107.WIP_GROUP,
            r109_latest.TEST_GROUP,
            r109_latest.TEST_TIME,
            r109_latest.TEST_CODE,
            error_desc.ERROR_DESC,
            repair_task.DATA11,
            rt19.DATA19_COMBINED,
            chkin.first_checkin_date AS CHECKIN_DATE,
            TRUNC(SYSDATE - chkin.first_checkin_date) AS AGING_DAY
        FROM sfism4.r107 r107
        JOIN sfis1.c_model_desc_t model_desc
          ON r107.model_name = model_desc.model_name
        LEFT JOIN sfism4.r_repair_task_t repair_task
          ON r107.SERIAL_NUMBER = repair_task.SERIAL_NUMBER
        LEFT JOIN (
            SELECT d.SERIAL_NUMBER, MIN(d.DATE3) AS first_checkin_date
            FROM sfism4.R_REPAIR_TASK_DETAIL_T d
            WHERE d.DATA12 = 'CHECK_IN'
            GROUP BY d.SERIAL_NUMBER
        ) chkin
          ON chkin.SERIAL_NUMBER = r107.SERIAL_NUMBER
        LEFT JOIN (
            SELECT d.SERIAL_NUMBER,
                   LISTAGG(TRIM(d.DATA19), ' | ') WITHIN GROUP (ORDER BY d.DATE3) AS DATA19_COMBINED
            FROM sfism4.R_REPAIR_TASK_DETAIL_T d
            WHERE UPPER(d.DATA17) IN ('CONFIRM','SAVE')
              AND d.DATA19 IS NOT NULL
            GROUP BY d.SERIAL_NUMBER
        ) rt19
          ON rt19.SERIAL_NUMBER = r107.SERIAL_NUMBER
        LEFT JOIN (
            SELECT SERIAL_NUMBER, TEST_CODE, TEST_TIME, TEST_GROUP
            FROM (
                SELECT R109.SERIAL_NUMBER,
                       R109.TEST_CODE,
                       R109.TEST_TIME,
                       R109.TEST_GROUP,
                       ROW_NUMBER() OVER(PARTITION BY R109.SERIAL_NUMBER ORDER BY R109.TEST_TIME DESC, R109.TEST_CODE DESC) AS rn
                FROM SFISM4.R109 R109
            ) t
            WHERE t.rn = 1
        ) r109_latest
          ON r109_latest.SERIAL_NUMBER = r107.SERIAL_NUMBER
        INNER JOIN sfis1.C_ERROR_CODE_T error_desc
          ON r109_latest.TEST_CODE = error_desc.ERROR_CODE
        WHERE model_desc.MODEL_SERIAL = 'ADAPTER'
          AND r107.SERIAL_NUMBER NOT IN (SELECT SERIAL_NUMBER FROM SFISM4.Z_KANBAN_TRACKING_T)
          AND r107.MO_NUMBER LIKE '4%'
          AND r107.WIP_GROUP LIKE '%B28M%'
          AND r107.ERROR_FLAG IN ('7','8')
          AND r109_latest.TEST_CODE NOT IN (
            'BV00','PP10','BRK00','HSK00','SCR00','C028','TA00','CAR0','C010','C012',
            'LBxx','GB00','CLExx','GL00','BHSK00','DIR02','DIR03','DR00','GF06',
            'CLE02','CLE03','CLE04','CLE05','CLE06','CLE07','CLE08','LB01','LB02',
            'LB03','LB04','LB05','LB06','LB07','CK00'
          )";

            using var command = new OracleCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new RepairTaskResult
                {
                    SERIAL_NUMBER = reader["SERIAL_NUMBER"].ToString(),
                    MODEL_NAME = reader["MODEL_NAME"].ToString(),
                    PRODUCT_LINE = reader["PRODUCT_LINE"].ToString(),
                    MO_NUMBER = reader["MO_NUMBER"].ToString(),
                    ERROR_FLAG = reader["ERROR_FLAG"] != DBNull.Value ? reader["ERROR_FLAG"].ToString() : null,
                    WORK_FLAG = reader["WORK_FLAG"] != DBNull.Value ? reader["WORK_FLAG"].ToString() : null,
                    WIP_GROUP = reader["WIP_GROUP"] != DBNull.Value ? reader["WIP_GROUP"].ToString() : null,
                    TEST_GROUP = reader["TEST_GROUP"] != DBNull.Value ? reader["TEST_GROUP"].ToString() : null,
                    TEST_TIME = reader["TEST_TIME"]?.ToString(),
                    TEST_CODE = reader["TEST_CODE"]?.ToString(),
                    ERROR_DESC = reader["ERROR_DESC"]?.ToString(),
                    DATA11 = reader["DATA11"] != DBNull.Value ? reader["DATA11"].ToString() : null,
                    REPAIR = reader["DATA19_COMBINED"] != DBNull.Value ? reader["DATA19_COMBINED"].ToString() : null,
                    CHECKIN_DATE = reader["CHECKIN_DATE"] != DBNull.Value ? Convert.ToDateTime(reader["CHECKIN_DATE"]) : (DateTime?)null,
                    AGING_DAY = reader["AGING_DAY"] != DBNull.Value ? reader["AGING_DAY"].ToString() : null,
                });
            }

            return result;
        }


        /// <summary>
        /// Bonepile After
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        ///
        [HttpPost("bonepile-after-kanban-basic")]
        public async Task<IActionResult> BonepileAfterKanbanBasic([FromBody] StatusRequestBonepile request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { message = "Yeu c?u khong h?p l?!" });
                }
                bool filterByStatus = request.Statuses?.Any() == true;
                var statuses = filterByStatus ? request.Statuses.Where(s => !string.IsNullOrEmpty(s)).ToList() : null;
                var allData = await ExecuteBonepileAfterKanbanBasicQuery();

                var excludedSNs = GetExcludedSerialNumbers();
                if (excludedSNs.Any())
                {
                    allData = allData.Where(d => !excludedSNs.Contains(d.SERIAL_NUMBER?.Trim().ToUpper())).ToList();
                }

                var snList = allData
                    .Select(d => d.SERIAL_NUMBER?.Trim().ToUpper())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

                var scrapCategories = await _sqlContext.ScrapLists
                    .Where(s => snList.Contains(s.SN.Trim().ToUpper()))
                    .Select(s => new { SN = s.SN, ApplyTaskStatus = s.ApplyTaskStatus, TaskNumber = s.TaskNumber })
                    .ToListAsync();

                var scrapDict = scrapCategories.ToDictionary(
                    c => c.SN?.Trim().ToUpper() ?? "",
                    c => (ApplyTaskStatus: c.ApplyTaskStatus, TaskNumber: c.TaskNumber),
                    StringComparer.OrdinalIgnoreCase
                );

                var exportRecords = await _sqlContext.Exports
                    .Where(e => snList.Contains(e.SerialNumber.Trim().ToUpper()) && e.CheckingB36R > 0 && e.CheckingB36R < 3)
                    .ToListAsync();

                var exportDict = exportRecords
                    .GroupBy(e => e.SerialNumber?.Trim().ToUpper() ?? "")
                    .Select(g => g.OrderByDescending(e => e.ExportDate).First())
                    .ToDictionary(
                        e => e.SerialNumber.Trim().ToUpper(),
                        e => (e.CheckingB36R, e.ExportDate),
                        StringComparer.OrdinalIgnoreCase);

                var validStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "ScrapLackTask",
                    "ScrapHasTask",
                    "WaitingLink",
                    "Linked",
                    "WatitingScrap",
                    "ApproveBGA",
                    "WaitingApproveBGA",
                    "RepairInRE",
                };
                var result = allData
                    .Select(b =>
                    {
                        var sn = b.SERIAL_NUMBER?.Trim().ToUpper() ?? "";
                        string status;
                        if (scrapDict.TryGetValue(sn, out var scrapInfo))
                        {
                            var applyTaskStatus = scrapInfo.ApplyTaskStatus;

                            if (applyTaskStatus == 0 || applyTaskStatus == 1 || applyTaskStatus == 5 || applyTaskStatus == 6 || applyTaskStatus == 7)
                            {
                                status = string.IsNullOrEmpty(scrapInfo.TaskNumber) ? "ScrapLackTask" : "ScrapHasTask";
                            }
                            else if (applyTaskStatus == 2)
                            {
                                status = "WatitingScrap";
                            }
                            else if (applyTaskStatus == 3)
                            {
                                status = "ApproveBGA";
                            }
                            else if (applyTaskStatus == 4)
                            {
                                status = "WaitingApproveBGA";
                            }
                            else
                            {
                                status = "RepairInRE";
                            }
                        }
                        else
                        {
                            if (exportDict.TryGetValue(sn, out var exportInfo))
                            {
                                status = exportInfo.CheckingB36R switch
                                {
                                    1 => "WaitingLink",
                                    2 => "Linked",
                                    _ => "RepairInRE",
                                };
                            }
                            else
                            {
                                status = "RepairInRE";
                            }
                        }
                        return new
                        {
                            SN = b.SERIAL_NUMBER,
                            ModelName = b.MODEL_NAME,
                            MoNumber = b.MO_NUMBER,
                            ProductLine = b.PRODUCT_LINE,
                            WipGroupSFC = b.WIP_GROUP_SFC,
                            WipGroupKANBAN = b.WIP_GROUP_KANBAN,
                            ErrorFlag = b.ERROR_FLAG,
                            WorkFlag = b.WORK_FLAG,
                            Status = status
                        };
                    })
                    .Where(r => validStatuses.Contains(r.Status, StringComparer.OrdinalIgnoreCase) &&
                                (!filterByStatus || statuses.Contains(r.Status, StringComparer.OrdinalIgnoreCase)))
                    .ToList();
                // Log result count
                Console.WriteLine($"Result Count: {result?.Count}");

                if (!result.Any())
                {
                    return NotFound(new { message = "Khong tim th?y d? li?u!", count = 0 });
                }

                return Ok(new
                {
                    count = result?.Count,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Xay ra loi", error = ex.Message });
            }
        }

        [HttpPost("bonepile-after-kanban-testinfo")]
        public async Task<IActionResult> BonepileAfterKanbanTestInfo([FromBody] SerialNumberBonepileRequest request)
        {
            try
            {
                if (request?.SerialNumbers == null || request.SerialNumbers.Count == 0)
                {
                    return BadRequest(new { message = "Danh sach SN khong h?p l?!" });
                }

                var data = await ExecuteBonepileAfterKanbanTestInfoQuery(request.SerialNumbers);

                return Ok(new
                {
                    count = data.Count,
                    data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Xay ra loi", error = ex.Message });
            }
        }

        [HttpGet("bonepile-after-kanban-count")]
        public async Task<IActionResult> BonepileAfterKanbanCount()
        {
            try
            {
                var repairTaskData = await ExecuteBonepileAfterKanbanBasicQuery();

                var excludedSNs = GetExcludedSerialNumbers();
                if (excludedSNs.Any())
                {
                    repairTaskData = repairTaskData.Where(d => !excludedSNs.Contains(d.SERIAL_NUMBER?.Trim().ToUpper())).ToList();
                }

                var snList = repairTaskData
                    .Select(d => d.SERIAL_NUMBER?.Trim().ToUpper())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

                var scrapCategories = await _sqlContext.ScrapLists
                    .Where(s => snList.Contains(s.SN.Trim().ToUpper()))
                    .Select(s => new { SN = s.SN, ApplyTaskStatus = s.ApplyTaskStatus, TaskNumber = s.TaskNumber })
                    .ToListAsync();
                var scrapDict = scrapCategories.ToDictionary(
                    c => c.SN?.Trim().ToUpper() ?? "",
                    c => (ApplyTaskStatus: c.ApplyTaskStatus, TaskNumber: c.TaskNumber),
                    StringComparer.OrdinalIgnoreCase
                );

                var exportRecords = await _sqlContext.Exports
                    .Where(e => snList.Contains(e.SerialNumber.Trim().ToUpper()) && e.CheckingB36R > 0 && e.CheckingB36R < 3)
                    .ToListAsync();

                var exportDict = exportRecords
                    .GroupBy(e => e.SerialNumber?.Trim().ToUpper() ?? "")
                    .Select(g => g.OrderByDescending(e => e.ExportDate).First())
                    .ToDictionary(
                        e => e.SerialNumber.Trim().ToUpper(),
                        e => (e.CheckingB36R, e.ExportDate),
                        StringComparer.OrdinalIgnoreCase);

                var result = repairTaskData.Select(b =>
                {
                    var sn = b.SERIAL_NUMBER?.Trim().ToUpper() ?? "";
                    string status;

                    if (scrapDict.TryGetValue(sn, out var scrapInfo))
                    {
                        var applyTaskStatus = scrapInfo.ApplyTaskStatus;

                        if (applyTaskStatus == 0 || applyTaskStatus == 1 || applyTaskStatus == 5 || applyTaskStatus == 6 || applyTaskStatus == 7)
                        {
                            status = string.IsNullOrEmpty(scrapInfo.TaskNumber) ? "ScrapLackTask" : "ScrapHasTask";
                        }
                        else if (applyTaskStatus == 2)
                        {
                            status = "WatitingScrap";
                        }
                        else if (applyTaskStatus == 3)
                        {
                            status = "ApproveBGA";
                        }
                        else if (applyTaskStatus == 4)
                        {
                            status = "WaitingApproveBGA";
                        }
                        else
                        {
                            status = "RepairInRE";
                        }
                    }
                    else
                    {
                        if (exportDict.TryGetValue(sn, out var exportInfo))
                        {
                            status = exportInfo.CheckingB36R switch
                            {
                                1 => "WaitingLink",
                                2 => "Linked",
                                _ => "RepairInRE",
                            };
                        }
                        else
                        {
                            status = "RepairInRE";
                        }
                    }
                    return status;
                }).ToList();
                var statusCounts = result
                    .GroupBy(status => status)
                    .Select(g => new
                    {
                        Status = g.Key,
                        Count = g.Count()
                    })
                    .ToList();
                return Ok(new
                {
                    totalCount = result.Count,
                    statusCounts = statusCounts
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "X?y ra l?i", error = ex.Message });
            }
        }

        [HttpGet("bonepile-after-kanban-aging-count")]
        public async Task<IActionResult> BonepileAfterKanbanAgingCount()
        {
            try
            {
                var basicData = await ExecuteBonepileAfterKanbanBasicQuery();

                var excludedSNs = GetExcludedSerialNumbers();
                if (excludedSNs.Any())
                {
                    basicData = basicData.Where(d => !excludedSNs.Contains(d.SERIAL_NUMBER?.Trim().ToUpper())).ToList();
                }

                var snList = basicData
                    .Select(d => d.SERIAL_NUMBER?.Trim().ToUpper())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

                var testInfos = await ExecuteBonepileAfterKanbanTestInfoQuery(snList);
                var testInfoDict = testInfos.ToDictionary(t => t.SERIAL_NUMBER?.Trim().ToUpper() ?? "", StringComparer.OrdinalIgnoreCase);

                var records = basicData
                    .Select(b =>
                    {
                        testInfoDict.TryGetValue(b.SERIAL_NUMBER?.Trim().ToUpper() ?? "", out var t);
                        return new
                        {
                            SN = b.SERIAL_NUMBER,
                            ModelName = b.MODEL_NAME,
                            MoNumber = b.MO_NUMBER,
                            ProductLine = b.PRODUCT_LINE,
                            WipGroupSFC = b.WIP_GROUP_SFC,
                            WipGroupKANBAN = b.WIP_GROUP_KANBAN,
                            testTime = t?.TEST_TIME,
                            testCode = t?.TEST_CODE,
                            errorDesc = t?.ERROR_DESC,
                            testGroup = t?.TEST_GROUP,
                            errorFlag = b.ERROR_FLAG,
                            aging = t?.AGING
                        };
                    })
                    .ToList();

                var agingGroups = records
                    .GroupBy(r =>
                    {
                        if (r.aging.HasValue)
                        {
                            var aging = r.aging.Value;
                            if (aging < 30) return "<30";
                            if (aging <= 90) return "30-90";
                            return ">90";
                        }
                        return ">90";
                    })
                    .Select(g => new
                    {
                        AgeRange = g.Key,
                        Count = g.Count(),
                        Records = g.ToList()
                    })
                    .ToList();

                return Ok(new
                {
                    totalCount = records.Count,
                    agingCounts = agingGroups
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "X?y ra l?i", error = ex.Message });
            }
        }

        private async Task<List<BonepileAfterKanbanResult>> ExecuteBonepileAfterKanbanBasicQuery()
        {
            var result = new List<BonepileAfterKanbanResult>();

            await using var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
            await connection.OpenAsync();

            string query = @"
                SELECT
                    A.SERIAL_NUMBER,
                    R107.MO_NUMBER,
                    A.MODEL_NAME,
                    B.PRODUCT_LINE,
                    A.WIP_GROUP AS WIP_GROUP_KANBAN,
                    R107.WIP_GROUP AS WIP_GROUP_SFC,
                    R107.ERROR_FLAG,
                    R107.WORK_FLAG
                FROM SFISM4.Z_KANBAN_TRACKING_T A
                JOIN SFIS1.C_MODEL_DESC_T B
                  ON A.MODEL_NAME = B.MODEL_NAME
                JOIN SFISM4.R107 R107
                  ON R107.SERIAL_NUMBER = A.SERIAL_NUMBER
                WHERE
                    A.WIP_GROUP LIKE '%B36R%'
                    AND B.MODEL_SERIAL = 'ADAPTER'
                    AND R107.WIP_GROUP NOT LIKE '%BR2C%'
                    AND R107.WIP_GROUP NOT LIKE '%BCFA%'";

            using var command = new OracleCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new BonepileAfterKanbanResult
                {
                    SERIAL_NUMBER = reader["SERIAL_NUMBER"]?.ToString(),
                    MO_NUMBER = reader["MO_NUMBER"]?.ToString(),
                    MODEL_NAME = reader["MODEL_NAME"]?.ToString(),
                    PRODUCT_LINE = reader["PRODUCT_LINE"]?.ToString(),
                    WIP_GROUP_KANBAN = reader["WIP_GROUP_KANBAN"]?.ToString(),
                    WIP_GROUP_SFC = reader["WIP_GROUP_SFC"]?.ToString(),
                    ERROR_FLAG = reader["ERROR_FLAG"]?.ToString(),
                    WORK_FLAG = reader["WORK_FLAG"]?.ToString()
                });
            }
            return result;
        }

        private async Task<List<BonepileAfterKanbanTestInfoResult>> ExecuteBonepileAfterKanbanTestInfoQuery(List<string> serialNumbers)
        {
            var result = new List<BonepileAfterKanbanTestInfoResult>();
            if (serialNumbers == null || serialNumbers.Count == 0)
            {
                return result;
            }

            await using var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
            await connection.OpenAsync();

            var batchSize = 1000; // Tránh lỗi ORA-01795 bằng cách chia nhỏ danh sách
            for (int i = 0; i < serialNumbers.Count; i += batchSize)
            {
                var batch = serialNumbers.Skip(i).Take(batchSize).ToList();
                var parameterNames = batch.Select((sn, idx) => $":sn{idx}").ToList();
                var inClause = string.Join(",", parameterNames);

                string query = $@"
                SELECT
                    A.SERIAL_NUMBER,
                    COALESCE(C.TEST_GROUP, R109X.LATEST_TEST_GROUP) AS TEST_GROUP,
                    COALESCE(C.TEST_TIME , R109X.LATEST_TEST_TIME ) AS TEST_TIME,
                    COALESCE(C.TEST_CODE , R109X.LATEST_TEST_CODE ) AS TEST_CODE,
                    COALESCE(E1.ERROR_DESC, E2.ERROR_DESC) AS ERROR_DESC,
                    TRUNC(SYSDATE) - TRUNC(COALESCE(C.TEST_TIME, R109X.LATEST_TEST_TIME)) AS AGING
                FROM SFISM4.Z_KANBAN_TRACKING_T A
                LEFT JOIN SFISM4.R_REPAIR_TASK_T C
                  ON C.SERIAL_NUMBER = A.SERIAL_NUMBER
                LEFT JOIN (
                    SELECT
                        K.KEY_PART_SN,
                        MAX(K.SERIAL_NUMBER) KEEP (DENSE_RANK LAST ORDER BY K.WORK_TIME) AS PARENT_SN
                    FROM SFISM4.P_WIP_KEYPARTS_T K
                    WHERE K.WORK_TIME IS NOT NULL
                    GROUP BY K.KEY_PART_SN
                ) KP
                  ON KP.KEY_PART_SN = A.SERIAL_NUMBER
                LEFT JOIN (
                    SELECT
                        R.SERIAL_NUMBER,
                        MAX(R.TEST_TIME) AS LATEST_TEST_TIME,
                        MAX(R.TEST_CODE) KEEP (DENSE_RANK LAST ORDER BY R.TEST_TIME) AS LATEST_TEST_CODE,
                        MAX(R.TEST_GROUP) KEEP (DENSE_RANK LAST ORDER BY R.TEST_TIME) AS LATEST_TEST_GROUP
                    FROM SFISM4.R109 R
                    WHERE R.TEST_TIME IS NOT NULL
                    GROUP BY R.SERIAL_NUMBER
                ) R109X
                  ON R109X.SERIAL_NUMBER = KP.PARENT_SN
                LEFT JOIN SFIS1.C_ERROR_CODE_T E1 ON C.TEST_CODE = E1.ERROR_CODE
                LEFT JOIN SFIS1.C_ERROR_CODE_T E2 ON R109X.LATEST_TEST_CODE = E2.ERROR_CODE
                WHERE A.SERIAL_NUMBER IN ({inClause})";

                using var command = new OracleCommand(query, connection);
                for (int j = 0; j < batch.Count; j++)
                {
                    command.Parameters.Add(parameterNames[j], OracleDbType.Varchar2).Value = batch[j];
                }

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new BonepileAfterKanbanTestInfoResult
                    {
                        SERIAL_NUMBER = reader["SERIAL_NUMBER"]?.ToString(),
                        TEST_GROUP = reader["TEST_GROUP"]?.ToString(),
                        TEST_TIME = reader["TEST_TIME"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["TEST_TIME"]),
                        TEST_CODE = reader["TEST_CODE"]?.ToString(),
                        ERROR_DESC = reader["ERROR_DESC"]?.ToString(),
                        AGING = reader["AGING"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["AGING"])
                    });
                }
            }

            return result;
        }

        //Lay nhung SN scrap trong file excel.
        private List<string> GetExcludedSerialNumbers()
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            var filePath = Path.Combine(uploadsFolder, "ScrapOk.xlsx");
            var snList = new List<string>();

            if (System.IO.File.Exists(filePath))
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using var package = new ExcelPackage(new FileInfo(filePath));
                var worksheet = package.Workbook.Worksheets.FirstOrDefault();

                if (worksheet != null)
                {
                    int rowCount = worksheet.Dimension.Rows;
                    for (int row = 1; row <= rowCount; row++)
                    {
                        var sn = worksheet.Cells[row, 1].Text.Trim();
                        if (!string.IsNullOrEmpty(sn))
                        {
                            snList.Add(sn.ToUpper());
                        }
                    }
                }
            }

            return snList;
        }

        [HttpPost("adapter-mo-records")]
        public async Task<IActionResult> AdapterMoRecords([FromBody] StatusRequestBonepile request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { message = "Yeu c?u khong h?p l?!" });
                }

                bool filterByStatus = request.Statuses?.Any() == true;
                var statuses = filterByStatus ? request.Statuses.Where(s => !string.IsNullOrEmpty(s)).ToList() : null;

                // L?y data t? Oracle
                var allData = await ExecuteAdapterMoQuery();

                // L?y ApplyTaskStatus t? ScrapLists
                var scrapCategories = await _sqlContext.ScrapLists
                    .Select(s => new { SN = s.SN, ApplyTaskStatus = s.ApplyTaskStatus, TaskNumber = s.TaskNumber })
                    .ToListAsync();

                var scrapDict = scrapCategories.ToDictionary(
                    c => c.SN?.Trim().ToUpper() ?? "",
                    c => (ApplyTaskStatus: c.ApplyTaskStatus, TaskNumber: c.TaskNumber),
                    StringComparer.OrdinalIgnoreCase
                );

                // ??nh ngh?a validStatuses
                var validStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Scrap Lacks Task",
                    "Scrap Has Scrap",
                    "Approved BGA",
                    "Waiting approve BGA",
                    "Waiting approve scrap",
                    "Ch? Link",
                    "?a M? MO"
                };

                // Phan lo?i status
                var result = allData
                    .Select(b =>
                    {
                        var sn = b.SERIAL_NUMBER?.Trim().ToUpper() ?? "";
                        string status;

                        // Ki?m tra thong tin trong scrapDict
                        if (scrapDict.TryGetValue(sn, out var scrapInfo))
                        {
                            var applyTaskStatus = scrapInfo.ApplyTaskStatus;
                            var taskNumber = scrapInfo.TaskNumber;

                            if (applyTaskStatus == 0 || applyTaskStatus == 1 || applyTaskStatus == 5 || applyTaskStatus == 6 || applyTaskStatus == 7)
                            {
                                status = string.IsNullOrEmpty(taskNumber) ? "Scrap Lacks Task" : "Scrap Has Scrap";
                            }
                            else
                            {
                                status = applyTaskStatus switch
                                {
                                    2 => "Waiting approve scrap",
                                    3 => "Approved BGA",
                                    4 => "Waiting approve BGA",
                                    _ => b.MO_STATUS
                                };
                            }
                        }
                        else
                        {
                            status = b.MO_STATUS;
                        }

                        return new
                        {
                            SN = b.SERIAL_NUMBER,
                            ModelName = b.MODEL_NAME,
                            MoNumber = b.MO_NUMBER,
                            ProductLine = b.PRODUCT_LINE,
                            WipGroup = b.WIP_GROUP,
                            TestGroup = b.TEST_GROUP,
                            TestCode = b.TEST_CODE,
                            TestTime = b.TEST_TIME,
                            Status = status
                        };
                    })
                    .Where(r => validStatuses.Contains(r.Status, StringComparer.OrdinalIgnoreCase) &&
                                (!filterByStatus || statuses.Contains(r.Status, StringComparer.OrdinalIgnoreCase)))
                    .ToList();

                Console.WriteLine($"Result Count: {result?.Count}");

                if (!result.Any())
                {
                    return NotFound(new { message = "Khong tim th?y d? li?u!", count = 0 });
                }

                return Ok(new
                {
                    count = result?.Count,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "X?y ra l?i", error = ex.Message });
            }
        }

        [HttpGet("adapter-mo-status-count")]
        public async Task<IActionResult> AdapterMoStatusCount()
        {
            try
            {
                var moData = await ExecuteAdapterMoQuery();
                var scrapCategories = await _sqlContext.ScrapLists
                    .Select(s => new { SN = s.SN, ApplyTaskStatus = s.ApplyTaskStatus, TaskNumber = s.TaskNumber })
                    .ToListAsync();

                var scrapDict = scrapCategories.ToDictionary(
                    c => c.SN?.Trim().ToUpper() ?? "",
                    c => (ApplyTaskStatus: c.ApplyTaskStatus, TaskNumber: c.TaskNumber),
                    StringComparer.OrdinalIgnoreCase
                );

                // Phan lo?i tr?ng thai
                var result = moData
                    .Select(b =>
                    {
                        var sn = b.SERIAL_NUMBER?.Trim().ToUpper() ?? "";
                        string status;

                        if (scrapDict.TryGetValue(sn, out var scrapInfo))
                        {
                            var applyTaskStatus = scrapInfo.ApplyTaskStatus;
                            var taskNumber = scrapInfo.TaskNumber;

                            if (applyTaskStatus == 0 || applyTaskStatus == 1 || applyTaskStatus == 5 || applyTaskStatus == 6 || applyTaskStatus == 7)
                            {
                                status = string.IsNullOrEmpty(taskNumber) ? "Scrap Lacks Task" : "Scrap Has Scrap";
                            }
                            else
                            {
                                status = applyTaskStatus switch
                                {
                                    2 => "Waiting approve scrap",
                                    3 => "Approved BGA",
                                    4 => "Waiting approve BGA",
                                    _ => b.MO_STATUS
                                };
                            }
                        }
                        else
                        {
                            status = b.MO_STATUS;
                        }

                        return status;
                    })
                    .ToList();

                var statusCounts = result
                    .GroupBy(status => status)
                    .Select(g => new
                    {
                        Status = g.Key,
                        Count = g.Count()
                    })
                    .ToList();

                return Ok(new
                {
                    totalCount = result.Count,
                    statusCounts = statusCounts
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "X?y ra l?i", error = ex.Message });
            }
        }

        private async Task<List<MoTaskResult>> ExecuteAdapterMoQuery()
        {
            var result = new List<MoTaskResult>();

            await using var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
            await connection.OpenAsync();

            string query = @"
                SELECT 
                    r107.SERIAL_NUMBER,
                    r107.MODEL_NAME,
                    model_desc.PRODUCT_LINE,
                    r107.WIP_GROUP,
                    r107.MO_NUMBER,
                    R109.TEST_GROUP,
                    R109.TEST_CODE,
                    R109.TEST_TIME,
                    'CH? LINK' AS MO_STATUS
                FROM 
                    SFISM4.R107 r107
                INNER JOIN 
                    SFIS1.C_MODEL_DESC_T model_desc
                    ON r107.MODEL_NAME = model_desc.MODEL_NAME
                LEFT JOIN (
                    SELECT 
                        SERIAL_NUMBER,
                        TEST_CODE,
                        TEST_TIME,
                        TEST_GROUP,
                        ROW_NUMBER() OVER (PARTITION BY SERIAL_NUMBER ORDER BY TEST_TIME DESC) AS rn
                    FROM SFISM4.R109
                ) R109
                    ON r107.SERIAL_NUMBER = R109.SERIAL_NUMBER AND R109.rn = 1
                WHERE 
                    r107.WIP_GROUP LIKE '%B31M%'
                    AND model_desc.MODEL_SERIAL = 'ADAPTER'
                    AND r107.MODEL_NAME NOT LIKE '900%'
                    AND r107.MODEL_NAME NOT LIKE '692%'

                UNION ALL

                SELECT 
                    r107.SERIAL_NUMBER,
                    r107.MO_NUMBER,
                    r107.MODEL_NAME,
                    r107.WIP_GROUP,
                    model_desc.PRODUCT_LINE,
                    R109.TEST_GROUP,
                    R109.TEST_CODE,
                    R109.TEST_TIME,
                    '?a M? MO' AS MO_STATUS
                FROM 
                    SFISM4.R107 r107
                INNER JOIN 
                    SFISM4.R105 r105
                    ON r107.MO_NUMBER = r105.MO_NUMBER
                INNER JOIN 
                    SFIS1.C_MODEL_DESC_T model_desc
                    ON r107.MODEL_NAME = model_desc.MODEL_NAME
                LEFT JOIN (
                    SELECT 
                        SERIAL_NUMBER,
                        TEST_CODE,
                        TEST_TIME,
                        TEST_GROUP,
                        ROW_NUMBER() OVER (PARTITION BY SERIAL_NUMBER ORDER BY TEST_TIME DESC) AS rn
                    FROM SFISM4.R109
                ) R109
                    ON r107.SERIAL_NUMBER = R109.SERIAL_NUMBER AND R109.rn = 1
                INNER JOIN (
                    SELECT SERIAL_NUMBER
                    FROM (
                        SELECT 
                            SERIAL_NUMBER,
                            GROUP_NAME,
                            ROW_NUMBER() OVER (PARTITION BY SERIAL_NUMBER ORDER BY IN_STATION_TIME DESC) AS rn
                        FROM SFISM4.R117
                    ) t
                    WHERE rn = 1 AND GROUP_NAME = 'LINK_MO'
                ) r117_latest
                    ON r107.SERIAL_NUMBER = r117_latest.SERIAL_NUMBER
                INNER JOIN (
                    SELECT SERIAL_NUMBER
                    FROM (
                        SELECT 
                            SERIAL_NUMBER,
                            WIP_GROUP,
                            ROW_NUMBER() OVER (PARTITION BY SERIAL_NUMBER ORDER BY IN_STATION_TIME DESC) AS rn
                        FROM SFISM4.R117
                    ) t
                    WHERE rn = 2 AND WIP_GROUP LIKE '%B31M%'
                ) r117_second
                    ON r107.SERIAL_NUMBER = r117_second.SERIAL_NUMBER
                WHERE 
                    r107.WIP_GROUP NOT IN ('KANBAN_IN', 'STOCKIN')
                    AND r107.WIP_GROUP NOT LIKE '%B28M%'
                    AND r107.WIP_GROUP NOT LIKE '%B30M%'
                    AND r107.WIP_GROUP NOT LIKE '%BR2C%'
                    AND r107.WIP_GROUP NOT LIKE '%B31M%'
                    AND r107.MO_NUMBER LIKE '3%'
                    AND r107.MODEL_NAME NOT LIKE '900%'
                    AND r107.MODEL_NAME NOT LIKE '692%'
                    AND r105.CLOSE_FLAG = 2
                    AND model_desc.MODEL_SERIAL = 'ADAPTER'
            ";

            using (var command = new OracleCommand(query, connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.Add(new MoTaskResult
                        {
                            SERIAL_NUMBER = reader["SERIAL_NUMBER"].ToString(),
                            MODEL_NAME = reader["MODEL_NAME"].ToString(),
                            PRODUCT_LINE = reader["PRODUCT_LINE"].ToString(),
                            WIP_GROUP = reader["WIP_GROUP"].ToString(),
                            MO_NUMBER = reader["MO_NUMBER"].ToString(),
                            TEST_GROUP = reader["TEST_GROUP"] != DBNull.Value ? reader["TEST_GROUP"].ToString() : null,
                            TEST_CODE = reader["TEST_CODE"] != DBNull.Value ? reader["TEST_CODE"].ToString() : null,
                            TEST_TIME = reader["TEST_TIME"] != DBNull.Value ? Convert.ToDateTime(reader["TEST_TIME"]) : (DateTime?)null,
                            MO_STATUS = reader["MO_STATUS"].ToString()
                        });
                    }
                }
            }

            return result;
        }
    }

    public class MoTaskResult
    {
        public string SERIAL_NUMBER { get; set; }
        public string MODEL_NAME { get; set; }
        public string PRODUCT_LINE { get; set; }
        public string WIP_GROUP { get; set; }
        public string MO_NUMBER { get; set; }
        public string TEST_GROUP { get; set; }
        public string TEST_CODE { get; set; }
        public DateTime? TEST_TIME { get; set; }
        public string MO_STATUS { get; set; }
    }

}
