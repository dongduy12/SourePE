#nullable disable
using API_WEB.Controllers.Repositories;
using API_WEB.Models.Repositories;
using API_WEB.Models.SmartFA;
using API_WEB.ModelsDB;
using API_WEB.ModelsOracle;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using Microsoft.Extensions.Logging; // Thêm logging
using DocumentFormat.OpenXml.InkML;
using System.Linq;
using System.Collections.Generic;

namespace API_WEB.Controllers.SmartFA
{
    [Route("api/[controller]")]
    [ApiController]
    public class SearchFAController : ControllerBase
    {
        private const string V = "SearchProductsBySNInternal trả về success = false: {Message}";
        private readonly CSDL_NE _sqlContext;
        private readonly OracleDbContext _oracleContext;
        private readonly HttpClient _httpClient; // Thêm HttpClient để gọi API
        public SearchFAController(CSDL_NE sqlContext, OracleDbContext oracleContext, HttpClient httpClient)
        {
            _sqlContext = sqlContext ?? throw new ArgumentNullException(nameof(sqlContext));
            _oracleContext = oracleContext ?? throw new ArgumentNullException(nameof(oracleContext));
        }

        [HttpPost("search")]
        public async Task<IActionResult> SearchRepairTasks([FromBody] SearchRequestNe request)
        {
            try
            {
                // 1. Truy vấn OracleDataRepairTask và chỉ lấy SERIAL_NUMBER với MODEL_SERIAL != "SWITCH"
                var oracleTasks = await (
                    from task in _oracleContext.OracleDataRepairTask
                    join modelDesc in _oracleContext.OracleDataCModelDesc
                    on task.MODEL_NAME equals modelDesc.MODEL_NAME
                    join wipGroup in _oracleContext.OracleDataR107 // Join với bảng r107
                    on task.SERIAL_NUMBER equals wipGroup.SERIAL_NUMBER
                    join kanBanWIP in _oracleContext.OracleDataZKanbanTracking
                    on task.SERIAL_NUMBER equals kanBanWIP.SERIAL_NUMBER into kanBanJoin
                    from kanBanWIP in kanBanJoin.DefaultIfEmpty() // Left join để xử lý trường hợp SN không có trong z_kanban_tracking
                    where modelDesc.MODEL_SERIAL != "SWITCH" // Điều kiện lọc MODEL_SERIAL
                          && (string.IsNullOrEmpty(request.Data1) || EF.Functions.Like(task.DATA1, $"%{request.Data1}%"))
                          && (request.SerialNumbers == null || !request.SerialNumbers.Any() || request.SerialNumbers.Contains(task.SERIAL_NUMBER))
                          && (string.IsNullOrEmpty(request.ModelName) || task.MODEL_NAME == request.ModelName)
                          && (string.IsNullOrEmpty(request.TestCode) || task.TEST_CODE == request.TestCode)
                          && (string.IsNullOrEmpty(request.Status) || task.DATA11 == request.Status)
                          && (string.IsNullOrEmpty(request.HandoverStatus) || task.DATA13 == request.HandoverStatus)
                    select new
                    {
                        task.SERIAL_NUMBER,
                        task.MODEL_NAME,
                        task.MO_NUMBER,
                        task.TEST_GROUP,
                        task.TEST_CODE,
                        task.DATA1,
                        task.DATA11,
                        task.DATA12,
                        task.DATE3,
                        task.TESTER,
                        task.DATA13,
                        task.DATA17,
                        task.DATA18,
                        WIP_GROUP = wipGroup.WIP_GROUP, // Từ r107
                        KANBAN_WIP = kanBanWIP != null ? kanBanWIP.WIP_GROUP : null // Từ z_kanban_tracking, null nếu không tìm thấy
                    }
                ).ToListAsync();

                if (!oracleTasks.Any())
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Không tìm thấy dữ liệu phù hợp.",
                        data = new List<object>()
                    });
                }

                // 2. Xác định giá trị KANBAN_WIP dựa trên MODEL_NAME và z_kanban_tracking
                var tasksWithKanbanWip = oracleTasks.Select(task => new
                {
                    task.SERIAL_NUMBER,
                    task.MODEL_NAME,
                    task.MO_NUMBER,
                    task.TEST_GROUP,
                    task.TEST_CODE,
                    task.DATA1,
                    task.DATA11,
                    task.DATA12,
                    task.DATE3,
                    task.TESTER,
                    task.DATA13,
                    task.DATA17,
                    task.DATA18,
                    task.WIP_GROUP,
                    KANBAN_WIP = task.MODEL_NAME.StartsWith("900") ? "After" :
                                 (task.KANBAN_WIP != null ? task.KANBAN_WIP : "Before")
                }).ToList();

                // 3. Lấy danh sách ModelName từ tasksWithKanbanWip
                var modelNames = tasksWithKanbanWip.Select(t => t.MODEL_NAME).Distinct().ToList();

                // 4. Truy vấn ProductLine từ SFIS1.C_MODEL_DESC_T
                string productLineQuery = $@"
            SELECT MODEL_NAME, PRODUCT_LINE
            FROM SFIS1.C_MODEL_DESC_T
            WHERE MODEL_NAME IN ({string.Join(",", modelNames.Select(mn => $"'{mn}'"))})";

                var productLineResults = await _oracleContext.OracleDataCModelDesc
                    .FromSqlRaw(productLineQuery)
                    .AsNoTracking()
                    .Select(pl => new
                    {
                        MODEL_NAME = pl.MODEL_NAME,
                        PRODUCT_LINE = pl.PRODUCT_LINE ?? ""
                    })
                    .ToListAsync();

                // 5. Kết hợp ProductLine với tasksWithKanbanWip
                var oracleWithProductLine = tasksWithKanbanWip.Select(task => new
                {
                    task.SERIAL_NUMBER,
                    task.MODEL_NAME,
                    task.MO_NUMBER,
                    task.TEST_GROUP,
                    task.TEST_CODE,
                    task.DATA1,
                    task.DATA11,
                    task.DATA12,
                    task.DATE3,
                    task.TESTER,
                    task.DATA13,
                    task.DATA17,
                    task.DATA18,
                    task.WIP_GROUP,
                    task.KANBAN_WIP,
                    ProductLine = productLineResults.FirstOrDefault(pl => pl.MODEL_NAME == task.MODEL_NAME)?.PRODUCT_LINE ?? "N/A"
                });

                // 6. Truy vấn dữ liệu từ bảng Product và ScrapList (SQL Server)
                var serialNumbers = oracleWithProductLine.Select(t => t.SERIAL_NUMBER).ToList();
                var products = await _sqlContext.Products
                    .AsNoTracking()
                    .Include(p => p.Shelf)
                    .Where(product => serialNumbers.Contains(product.SerialNumber))
                    .Select(product => new
                    {
                        product.SerialNumber,
                        ShelfCode = product.Shelf != null ? product.Shelf.ShelfCode : null,
                        product.ColumnNumber,
                        product.LevelNumber,
                        product.TrayNumber,
                        product.PositionInTray,
                        product.BorrowStatus
                    })
                    .ToListAsync();

                //New query ScrapList
                var scrapList = await _sqlContext.ScrapLists.AsNoTracking().Where(scrap => serialNumbers.Contains(scrap.SN))
                    .Select(scrap => new
                    {
                        scrap.SN,
                        ScrapStatus = scrap.ApplyTaskStatus == 0 || scrap.ApplyTaskStatus == 1 ? "SPE Approved Scrap" :
                             scrap.ApplyTaskStatus == 2 ? "Waiting Approve" :
                             scrap.ApplyTaskStatus == 3 ? "SPE approve to BGA" : ""
                    }).ToListAsync();

                // 7. Kết hợp dữ liệu Oracle và SQL Server
                var combinedResults = oracleWithProductLine
                    .GroupJoin(
                        products,
                        oracle => oracle.SERIAL_NUMBER,
                        product => product.SerialNumber,
                        (oracle, productGroup) => new
                        {
                            OracleTask = oracle,
                            ProductData = productGroup.FirstOrDefault()
                        }
                    ).GroupJoin(
                    scrapList,
                        combined => combined.OracleTask.SERIAL_NUMBER,
                        scrap => scrap.SN,
                        (combined, scrapGroup) => new
                        {
                            combined.OracleTask,
                            combined.ProductData,
                            ScrapData = scrapGroup.FirstOrDefault()
                        }
                     )
                    .Select(result => new
                    {
                        result.OracleTask.SERIAL_NUMBER,
                        result.OracleTask.MODEL_NAME,
                        result.OracleTask.MO_NUMBER,
                        result.OracleTask.TEST_GROUP,
                        result.OracleTask.TEST_CODE,
                        result.OracleTask.DATA1,
                        result.OracleTask.DATA11,
                        result.OracleTask.DATA12,
                        result.OracleTask.DATE3,
                        result.OracleTask.TESTER,
                        result.OracleTask.DATA13,
                        result.OracleTask.DATA17,
                        result.OracleTask.DATA18,
                        result.OracleTask.WIP_GROUP,
                        result.OracleTask.KANBAN_WIP,
                        result.OracleTask.ProductLine,
                        ShelfCode = result.ProductData?.ShelfCode ?? "",
                        ColumnNumber = result.ProductData?.ColumnNumber,
                        LevelNumber = result.ProductData?.LevelNumber,
                        TrayNumber = result.ProductData?.TrayNumber,
                        PositionInTray = result.ProductData?.PositionInTray,
                        BorrowStatus = result.ProductData?.BorrowStatus,
                        ScrapStatus = result.ScrapData?.ScrapStatus ?? ""
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    totalResults = combinedResults.Count,
                    data = combinedResults
                });
            }
            catch (Exception ex)
            {
                // Ghi log chi tiết lỗi
                Console.WriteLine($"Lỗi: {ex.Message}");
                Console.WriteLine($"Chi tiết: {ex.StackTrace}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("get-fullname")]
        public IActionResult GetFullName(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return BadRequest(new { success = false, message = "Username không được để trống!" });
            }
            try
            {
                // Tìm FullName dua trên Username
                var user = _sqlContext.Users.FirstOrDefault(u => u.Username == username);

                if (user == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng!" });
                }

                return Ok(new { success = true, fullName = user.FullName });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });


            }
        }

        //Lay FullName theo trong ID
        [HttpPost("get-fullname-batch")]
        public IActionResult GetFullNameBatch([FromBody] List<string> usernames)
        {
            var users = _sqlContext.Users
                .Where(u => usernames.Contains(u.Username))
                .ToDictionary(u => u.Username, u => u.FullName);

            return Ok(new { success = true, data = users });
        }

        //Lay FullName theo list
        [HttpPost("get-fullname-batch-list")]
        public async Task<IActionResult> GetFullNameBatchList([FromBody] List<string> usernames)
        {
            if (usernames == null || usernames.Count == 0)
            {
                return BadRequest(new { success = false, message = "Danh sách ID không được để trống!" });
            }

            try
            {
                // Lay danh sách FullName tu SQL Server
                var users = await _sqlContext.Users
                    .Where(u => usernames.Contains(u.Username))
                    .ToDictionaryAsync(u => u.Username, u => u.FullName);

                return Ok(new { success = true, data = users });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("get-unique-modelnames")]
        public async Task<IActionResult> GetUniqueModelNames()
        {
            try
            {
                // Truy van lay danh sách ModelName khác nhau voi dieu kien MODEL_SERIAL != "SWITCH"
                var uniqueModelNames = await (
                    from task in _oracleContext.OracleDataRepairTask
                    join modelDesc in _oracleContext.OracleDataCModelDesc
                    on task.MODEL_NAME equals modelDesc.MODEL_NAME
                    where task.MODEL_NAME != null
                          && modelDesc.MODEL_SERIAL != "SWITCH" // Ðieu kien MODEL_SERIAL
                    select task.MODEL_NAME
                )
                .Distinct()
                .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = uniqueModelNames
                });
            }
            catch (Oracle.ManagedDataAccess.Client.OracleException ex)
            {
                // Xu lý loi liên quan den Oracle
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Oracle Error: {ex.Message}"
                });
            }
            catch (Exception ex)
            {
                // Xu lý loi chung
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpGet("get-unique-status")]
        public async Task<IActionResult> GetUniqueStatus()
        {
            try
            {
                // Truy van lay danh sách status khác nhau
                var uniqueStatus = await _oracleContext.OracleDataRepairTask
                    .Where(task => task.DATA11 != null && !task.MODEL_NAME.StartsWith("SA")) // Loai bo giá tri NULL
                    .Select(task => task.DATA11)
                    .Distinct()
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = uniqueStatus
                });
            }
            catch (Oracle.ManagedDataAccess.Client.OracleException ex)
            {
                //Xử lý liên quan đến oracle
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Oracle Error: {ex.Message}"
                });
            }
            catch (Exception ex)
            {
                //Xử lý lỗi chung
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpGet("get-unique-SN")]
        public async Task<IActionResult> GetUniqueSN()
        {
            try
            {
                var uniqueStatus = await (
                    from task in _oracleContext.OracleDataRepairTask
                    join modelDesc in _oracleContext.OracleDataCModelDesc
                    on task.MODEL_NAME equals modelDesc.MODEL_NAME
                    where task.SERIAL_NUMBER != null
                          && modelDesc.MODEL_SERIAL != "SWITCH"
                          && task.MODEL_NAME != null
                          && modelDesc.MODEL_NAME != null
                    select task.SERIAL_NUMBER
                )
                .Distinct()
                .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = uniqueStatus
                });
            }
            catch (Oracle.ManagedDataAccess.Client.OracleException ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Oracle Error: {ex.Message}"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpPost("search-history-by-list")]
        public async Task<IActionResult> GetHistoryBySerialNumberList([FromBody] List<string> serialNumbers)
        {
            try
            {
                if (serialNumbers == null || serialNumbers.Count == 0)
                {
                    return BadRequest(new { success = false, message = "Danh sách SN không được để trống!" });
                }

                // Truy v?n l?ch s? theo danh sách Serial Numbers
                var history = await _oracleContext.Set<R_REPAIR_TASK_DETAIL_T>()
                    .Where(detail => serialNumbers.Contains(detail.SERIAL_NUMBER))
                    .Select(detail => new
                    {
                        SERIAL_NUMBER = detail.SERIAL_NUMBER ?? "",
                        MO_NUMBER = detail.MO_NUMBER ?? "",
                        MODEL_NAME = detail.MODEL_NAME ?? "",
                        TEST_GROUP = detail.TEST_GROUP ?? "",
                        TEST_CODE = detail.TEST_CODE ?? "",
                        DATA1 = detail.DATA1 ?? "",
                        DATA11 = detail.DATA11 ?? "",
                        DATA12 = detail.DATA12 ?? "",
                        DATE3 = detail.DATE3,
                        TESTER = detail.TESTER ?? "",
                        DATA17 = detail.DATA17 ?? "",//type
                        DATA18 = detail.DATA18 ?? "",//Location
                        DATA19 = detail.DATA19 ?? "",//L?ch s? s?a ch?a.
                    })
                    .ToListAsync();

                if (history == null || history.Count == 0)
                {
                    return Ok(new { success = true, message = "Không tìm thấy lịch sử", data = new List<object>() });
                }

                return Ok(new { success = true, data = history });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("get-status-counts")]
        public async Task<IActionResult> GetStatusCounts([FromBody] string type)
        {
            try
            {
                IQueryable<dynamic> query;

                switch (type?.ToUpper())
                {
                    case "ALL":
                        query = _oracleContext.OracleDataRepairTask
                            .Where(task => task.DATA11 != null)
                            .Join(
                                _oracleContext.OracleDataCModelDesc,
                                task => task.MODEL_NAME,
                                modelDesc => modelDesc.MODEL_NAME,
                                (task, modelDesc) => new { task, modelDesc }
                            )
                            .Where(joined => joined.modelDesc.MODEL_SERIAL != "SWITCH")
                            .GroupBy(joined => joined.task.DATA11)
                            .Select(group => new
                            {
                                Status = group.Key,
                                Count = group.Count()
                            })
                            .OrderByDescending(x => x.Count); // ? S?p x?p gi?m d?n
                        break;

                    case "HANDOVER":
                        query = _oracleContext.OracleDataRepairTask
                            .Where(task => task.DATA13 != null)
                            .Join(
                                _oracleContext.OracleDataCModelDesc,
                                task => task.MODEL_NAME,
                                modelDesc => modelDesc.MODEL_NAME,
                                (task, modelDesc) => new { task, modelDesc }
                            )
                            .Where(joined => joined.modelDesc.MODEL_SERIAL != "SWITCH")
                            .GroupBy(joined => joined.task.DATA11)
                            .Select(group => new
                            {
                                Status = group.Key,
                                Count = group.Count()
                            })
                            .OrderByDescending(x => x.Count); // ? S?p x?p gi?m d?n
                        break;

                    case "ONLINE":
                        query = _oracleContext.OracleDataRepairTask
                            .Where(task => task.DATA18 != null && task.DATA18 != "TRONG_KHO" && task.DATA13 != "WAITING_HAND_OVER")
                            .Join(
                                _oracleContext.OracleDataCModelDesc,
                                task => task.MODEL_NAME,
                                modelDesc => modelDesc.MODEL_NAME,
                                (task, modelDesc) => new { task, modelDesc }
                            )
                            .Where(joined => joined.modelDesc.MODEL_SERIAL != "SWITCH")
                            .GroupBy(joined => joined.task.DATA11)
                            .Select(group => new
                            {
                                Status = group.Key,
                                Count = group.Count()
                            })
                            .OrderByDescending(x => x.Count); // ? S?p x?p gi?m d?n
                        break;

                    default:
                        return BadRequest(new
                        {
                            success = false,
                            message = "Type không hợp lệ. Vui lòng sử dụng: 'ALL', 'HANDOVER', hoặc 'ONLINE'."
                        });
                }

                // Th?c thi truy v?n và tr? v? k?t qu?
                var statusCounts = await query.ToListAsync();

                // ? Tính t?ng s? lu?ng n?u "ALL"
                int totalCount = (type.ToUpper() == "ALL") ? statusCounts.Sum(x => x.Count) : 0;

                return Ok(new
                {
                    success = true,
                    data = statusCounts,
                    totalCount = totalCount
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        //// API lấy dữ liệu xóa R_ theo owner
        //[HttpPost("get-repair-owner-data")]
        //public async Task<IActionResult> GetRepairOwnerData([FromBody] TimeRequest request)
        //{
        //    try
        //    {
        //        if (request == null || request.StartDate == null || request.EndDate == null)
        //        {
        //            return BadRequest(new
        //            {
        //                success = false,
        //                message = "Date không được để trống!"
        //            });
        //        }

        //        // Thực hiện truy vấn dữ liệu từ Entity Framework
        //        var repairOwnerData = await (
        //            from detail in _oracleContext.OracleDataRepairTaskDetail
        //            join repair in _oracleContext.OracleDataR109
        //            on detail.TESTER equals repair.REPAIRER
        //            join r107 in _oracleContext.OracleDataR107
        //            on repair.SERIAL_NUMBER equals r107.SERIAL_NUMBER
        //            where
        //                detail.DATE3 >= request.StartDate &&
        //                detail.DATE3 <= request.EndDate &&
        //                repair.REPAIR_TIME >= request.StartDate &&
        //                repair.REPAIR_TIME <= request.EndDate &&
        //                EF.Functions.Like(detail.DATA17.ToUpper(), "%NHAN%")
        //            select new
        //            {
        //                OWNER = detail.TESTER,
        //                repair.SERIAL_NUMBER,
        //                repair.MODEL_NAME,
        //                repair.TEST_GROUP,
        //                r107.WIP_GROUP,
        //                r107.MO_NUMBER,
        //                repair.TEST_CODE,
        //                repair.DATA1,
        //                REPAIRER = repair.REPAIRER,
        //                REPAIR_TIME = repair.REPAIR_TIME,
        //                repair.REASON_CODE,
        //                repair.ERROR_ITEM_CODE
        //            }
        //        ).Distinct().ToListAsync();

        //        return Ok(new
        //        {
        //            success = true,
        //            data = repairOwnerData
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new
        //        {
        //            success = false,
        //            message = ex.Message
        //        });
        //    }
        //}

        // API lấy dữ liệu xóa R_ theo owner
        [HttpPost("get-repair-owner-data")]
        public async Task<IActionResult> GetRepairOwnerData([FromBody] TimeRequest request)
        {
            try
            {
                if (request == null || request.StartDate == null || request.EndDate == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Date không được để trống!"
                    });
                }

                // Thực hiện truy vấn dữ liệu từ Entity Framework
                var repairOwnerData = await (
                    from detail in _oracleContext.OracleDataRepairTaskDetail
                    join repair in _oracleContext.OracleDataR109
                    on detail.TESTER equals repair.REPAIRER
                    join r107 in _oracleContext.OracleDataR107
                    on repair.SERIAL_NUMBER equals r107.SERIAL_NUMBER
                    where
                        detail.DATE3 >= request.StartDate &&
                        detail.DATE3 <= request.EndDate &&
                        repair.REPAIR_TIME >= request.StartDate &&
                        repair.REPAIR_TIME <= request.EndDate &&
                        EF.Functions.Like(detail.DATA17, "%Nhận%")
                    select new
                    {
                        OWNER = detail.TESTER,
                        repair.SERIAL_NUMBER,
                        repair.MODEL_NAME,
                        repair.TEST_GROUP,
                        r107.WIP_GROUP,
                        r107.MO_NUMBER,
                        r107.ERROR_FLAG,
                        repair.TEST_CODE,
                        repair.DATA1,
                        REPAIRER = repair.REPAIRER,
                        REPAIR_TIME = repair.REPAIR_TIME,
                        repair.REASON_CODE,
                        repair.ERROR_ITEM_CODE
                    }
                ).Distinct().ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = repairOwnerData
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // API lấy dữ liệu từ R109 theo SerialNumber với repair_time mới nhất
        [HttpPost("get-repair-owner-data-by-sn")]
        public async Task<IActionResult> GetRepairOwnerDataBySerial([FromBody] SerialNumberRequest request)
        {
            try
            {
                var serialNumbers = request?.SerialNumbers?
                    .Where(sn => !string.IsNullOrWhiteSpace(sn))
                    .Distinct()
                    .ToList() ?? new List<string>();

                if (!serialNumbers.Any())
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "SerialNumber không được để trống!",
                    });
                }

                const int batchSize = 1000;
                var allResults = new List<RepairOwnerBySerialResult>();

                for (int i = 0; i < serialNumbers.Count; i += batchSize)
                {
                    var batch = serialNumbers.Skip(i).Take(batchSize);
                    var serialList = string.Join(",", batch.Select(sn => $"'{sn.Replace("'", "''")}'"));

                    string query = $@"
                        SELECT SerialNumber, ModelName, ReasonCode, TestCode, ErrorDesc, Repairer, TestGroup, ErrorItemCode, RepairTime
                        FROM (
                            SELECT NVL(r.SERIAL_NUMBER,'') AS SerialNumber,
                                   NVL(r.MODEL_NAME,'') AS ModelName,
                                   NVL(r.REASON_CODE,'') AS ReasonCode,
                                   NVL(r.TEST_CODE,'') AS TestCode,
                                   NVL(c.ERROR_DESC,'') AS ErrorDesc,
                                   NVL(r.REPAIRER,' ') AS Repairer,
                                   NVL(r.TEST_GROUP,'') AS TestGroup,
                                   NVL(r.ERROR_ITEM_CODE,' ') AS ErrorItemCode,
                                   NVL(TO_CHAR(r.REPAIR_TIME,'YYYY/MM/DD HH24:MI:SS'),' ') AS RepairTime,
                                   ROW_NUMBER() OVER(PARTITION BY r.SERIAL_NUMBER ORDER BY NVL(r.REPAIR_TIME, TO_DATE('1970-01-01','YYYY-MM-DD')) DESC) AS RN
                            FROM SFISM4.R109 r
                            INNER JOIN SFIS1.C_ERROR_CODE_T c ON r.TEST_CODE = c.ERROR_CODE
                            WHERE r.REPAIR_TIME IS NOT NULL AND r.SERIAL_NUMBER IN ({serialList})
                        )
                        WHERE RN = 1";

                    var batchResults = await _oracleContext.Set<RepairOwnerBySerialResult>()
                        .FromSqlRaw(query)
                        .ToListAsync();

                    allResults.AddRange(batchResults);
                }

                return Ok(new
                {
                    success = true,
                    data = allResults
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // API lấy dữ liệu confirm và save theo theo thời gian.
        [HttpPost("get-confirm-save-data")]
        public async Task<IActionResult> GetConfirmSaveData([FromBody] TimeRequest request)
        {
            try
            {
                if (request.StartDate == null || request.EndDate == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Date không được để trống!"
                    });
                }

                var result = await _oracleContext.OracleDataRepairTaskDetail
                    .GroupJoin(_oracleContext.OracleDataR107,
                        task => task.SERIAL_NUMBER,
                        OracleDataR107 => OracleDataR107.SERIAL_NUMBER,
                        (task, r107Collection) => new { task, r107Collection })
                    .SelectMany(
                        x => x.r107Collection.DefaultIfEmpty(),
                        (task, OracleDataR107) => new { task.task, OracleDataR107 })
                    .Where(x =>
                        (EF.Functions.Like(x.task.DATA17.ToUpper(), "%CONFIRM%") || EF.Functions.Like(x.task.DATA17.ToUpper(), "%SAVE%")) &&
                        x.task.DATE3 >= request.StartDate && x.task.DATE3 <= request.EndDate && x.task.TESTER != null)
                    .Select(x => new
                    {
                        OWNER = x.task.TESTER,
                        x.task.SERIAL_NUMBER,
                        x.task.MODEL_NAME,
                        x.task.TEST_GROUP,
                        x.task.TEST_CODE,
                        x.task.DATA1,
                        TYPE = x.task.DATA17,
                        TIME_SAVE = x.task.DATE3,
                        CURRENT_STATUS = x.task.DATA12,
                        NEXT_STATUS = x.task.DATA11,
                        REMARK = x.task.DATA19,
                        WIP_GROUP = x.OracleDataR107 != null ? x.OracleDataR107.WIP_GROUP : null,
                        MO_NUMBER = x.OracleDataR107 != null ? x.OracleDataR107.MO_NUMBER : null,
                    })
                    .OrderBy(task => task.OWNER) // Sắp xếp theo OWNER
                    .ToListAsync();
                return Ok(new
                {
                    success = true,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // API tổng hợp summary owner theo thời gian
        [HttpPost("get-summary-owner")]
        public async Task<IActionResult> GetSummaryOwner([FromBody] TimeRequest timeRange)
        {
            try
            {
                using (var connection = new OracleConnection("User Id=TE;Password=B05te;Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=10.220.130.220)(PORT=1521)))(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME=vnsfc)));"))
                {
                    await connection.OpenAsync();

                    string query = @"
                SELECT 
                    COALESCE(B1.owner, COALESCE(B2.owner, COALESCE(B3.owner, B4.owner))) AS owner,
                    B1.NHAN,
                    B2.CONFIRM,
                    B3.SAVE,
                    B4.REPAIR
                FROM (
                    SELECT COUNT(serial_number) AS NHAN, tester AS owner
                    FROM SFISM4.R_REPAIR_TASK_DETAIL_T  
                    WHERE data17 LIKE '%Nhận%'  
                      AND date3 BETWEEN TO_DATE(:StartDate, 'YYYY-MM-DD HH24:MI:SS') 
                                   AND TO_DATE(:EndDate, 'YYYY-MM-DD HH24:MI:SS')
                    GROUP BY tester
                ) B1
                LEFT JOIN (
                    SELECT COUNT(a2.serial_number) AS CONFIRM, a2.tester AS owner
                    FROM (
                        SELECT aa.* 
                        FROM (
                            SELECT * 
                            FROM SFISM4.R_REPAIR_TASK_DETAIL_T  
                            WHERE UPPER(data17) LIKE '%CONFIRM%'  
                              AND date3 BETWEEN TO_DATE(:StartDate, 'YYYY-MM-DD HH24:MI:SS') 
                                          AND TO_DATE(:EndDate, 'YYYY-MM-DD HH24:MI:SS')
                            UNION ALL
                            SELECT * 
                            FROM SFISM4.R_REPAIR_TASK_DETAIL_T  
                            WHERE UPPER(data17) LIKE '%SAVE%'  
                              AND date3 BETWEEN TO_DATE(:StartDate, 'YYYY-MM-DD HH24:MI:SS') 
                                          AND TO_DATE(:EndDate, 'YYYY-MM-DD HH24:MI:SS') 
                              AND SERIAL_NUMBER NOT IN (
                                  SELECT SERIAL_NUMBER 
                                  FROM SFISM4.R_REPAIR_TASK_DETAIL_T  
                                  WHERE UPPER(data17) LIKE '%CONFIRM%'
                              )
                        ) aa 
                        WHERE (aa.serial_number, tester) NOT IN (
                            SELECT serial_number, repairer  
                            FROM sfism4.R109 
                            WHERE repair_time BETWEEN TO_DATE(:StartDate, 'YYYY-MM-DD HH24:MI:SS') 
                                                 AND TO_DATE(:EndDate, 'YYYY-MM-DD HH24:MI:SS')
                        )
                    ) a2 
                    WHERE UPPER(data17) LIKE '%CONFIRM%'    
                    GROUP BY tester
                ) B2 ON B1.owner = B2.owner
                LEFT JOIN (
                    SELECT COUNT(a3.serial_number) AS SAVE, a3.tester AS owner
                    FROM (
                        SELECT aa.* 
                        FROM (
                            SELECT * 
                            FROM SFISM4.R_REPAIR_TASK_DETAIL_T  
                            WHERE UPPER(data17) LIKE '%CONFIRM%'  
                            UNION ALL
                            SELECT * 
                            FROM SFISM4.R_REPAIR_TASK_DETAIL_T  
                            WHERE UPPER(data17) LIKE '%SAVE%'  
                              AND SERIAL_NUMBER NOT IN (
                                  SELECT SERIAL_NUMBER 
                                  FROM SFISM4.R_REPAIR_TASK_DETAIL_T  
                                  WHERE UPPER(data17) LIKE '%CONFIRM%'
                              )
                        ) aa 
                        WHERE (aa.serial_number, tester) NOT IN (
                            SELECT serial_number, repairer  
                            FROM sfism4.R109 
                            WHERE repair_time BETWEEN TO_DATE(:StartDate, 'YYYY-MM-DD HH24:MI:SS') 
                                                 AND TO_DATE(:EndDate, 'YYYY-MM-DD HH24:MI:SS')
                        )
                    ) a3 
                    WHERE UPPER(data17) LIKE '%SAVE%'    
                    GROUP BY tester
                ) B3 ON B1.owner = B3.owner
                LEFT JOIN (
                    SELECT COUNT(serial_number) AS REPAIR, repairer AS owner
                    FROM sfism4.R109
                    WHERE repair_time BETWEEN TO_DATE(:StartDate, 'YYYY-MM-DD HH24:MI:SS') 
                                                AND TO_DATE(:EndDate, 'YYYY-MM-DD HH24:MI:SS')  
                    GROUP BY repairer
                ) B4 ON B1.owner = B4.owner
                ORDER BY owner";

                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("StartDate", timeRange.StartDate.ToString("yyyy-MM-dd HH:mm:ss")));
                        command.Parameters.Add(new OracleParameter("EndDate", timeRange.EndDate.ToString("yyyy-MM-dd HH:mm:ss")));

                        var data = new List<dynamic>();
                        int totalNHAN = 0, totalCONFIRM = 0, totalSAVE = 0, totalREPAIR = 0;

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int nhan = reader["Nhận"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Nhận"]);
                                int confirm = reader["CONFIRM"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CONFIRM"]);
                                int save = reader["SAVE"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SAVE"]);
                                int repair = reader["REPAIR"] == DBNull.Value ? 0 : Convert.ToInt32(reader["REPAIR"]);

                                data.Add(new
                                {
                                    Owner = reader["owner"]?.ToString(),
                                    NHAN = nhan,
                                    CONFIRM = confirm,
                                    SAVE = save,
                                    REPAIR = repair
                                });

                                // C?ng d?n d? tính t?ng
                                totalNHAN += nhan;
                                totalCONFIRM += confirm;
                                totalSAVE += save;
                                totalREPAIR += repair;
                            }
                        }

                        return Ok(new
                        {
                            success = true,
                            data,
                            totals = new
                            {
                                Total_NHAN = totalNHAN,
                                Total_CONFIRM = totalCONFIRM,
                                Total_SAVE = totalSAVE,
                                Total_REPAIR = totalREPAIR
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpPost("get-allowed-areas")]
        public IActionResult GetAllowedAreas([FromBody] CardCodeRequest request)
        {
            if (string.IsNullOrEmpty(request.CardCode))
            {
                return BadRequest(new { success = false, message = "Mã th? không du?c d? tr?ng." });
            }

            try
            {
                // Tìm ngu?i dùng theo mã th? (Username)
                var user = _sqlContext.Users.FirstOrDefault(u => u.Username == request.CardCode);

                if (user == null)
                {
                    return NotFound(new { success = false, message = "Không tìm th?y ngu?i dùng v?i mã th? này." });
                }

                return Ok(new { success = true, allowedAreas = user.AllowedAreas });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("get-latest-tester")]
        public async Task<IActionResult> GetLatestTester([FromBody] List<string> serialNumbers)
        {
            if (serialNumbers == null || !serialNumbers.Any())
                return BadRequest(new { message = "Serial numbers list cannot be empty." });
            try
            {
                var serialNumbersList = string.Join(",", serialNumbers.Select(sn => $"'{sn}'"));
                // SQL Query ch? l?y SERIAL_NUMBER và TESTER
                string query = $@"
                    SELECT *
                        FROM (
                            SELECT
                                DETAIL_T.SERIAL_NUMBER,
                                DETAIL_T.TESTER,
                                DETAIL_T.DATE3,
                                DETAIL_T.DATA17
                            FROM SFISM4.R_REPAIR_TASK_DETAIL_T DETAIL_T
                            WHERE DETAIL_T.SERIAL_NUMBER IN ({serialNumbersList}) 
                            AND DETAIL_T.DATA17 = 'confirm'
                            ORDER BY DETAIL_T.DATE3 DESC
                        )
                        WHERE ROWNUM = 1
                        ";

                // K?t n?i co s? d? li?u Oracle và th?c hi?n truy v?n
                using (var command = _oracleContext.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = query;
                    command.CommandType = System.Data.CommandType.Text;

                    _oracleContext.Database.OpenConnection(); // M? k?t n?i

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var results = new List<SerialNumberTesterDto>();
                        while (await reader.ReadAsync())
                        {
                            results.Add(new SerialNumberTesterDto
                            {
                                SERIAL_NUMBER = reader["SERIAL_NUMBER"].ToString(),
                                TESTER = reader["TESTER"].ToString()
                            });
                        }

                        if (!results.Any())
                            return NotFound(new { message = "No records found for the given Serial Numbers!" });

                        return Ok(results);
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while processing the request.", error = ex.Message });
            }
        }

        //API get-retest-summary
        [HttpPost("get-retest-summary")]
        public async Task<IActionResult> GetRetestSummary([FromBody] TimeRequest request)
        {
            try
            {
                using (var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                SELECT 
                    tester AS ID,
                    COUNT(CASE WHEN test_result = 'PASS' THEN 1 END) AS QTY_OK,
                    COUNT(CASE WHEN test_result = 'FAIL' THEN 1 END) AS QTY_NG,
                    COUNT(CASE WHEN test_result = 'not data' THEN 1 END) AS NOT_DEFINE,
                    COUNT(*) AS TOTAL
                FROM (
                    SELECT 
                        tester,
                        NVL(REGEXP_SUBSTR(data19, 'KQ:([^/]+)', 1, 1, NULL, 1), 'not data') AS test_result
                    FROM sfism4.r_repair_task_detail_t 
                    WHERE data12 = 'RETEST'
                    AND data17 = 'confirm'
                    AND (data19 IS NULL OR data19 != 'CONFIRM_PUT_B36R') 
                    AND date3 BETWEEN TO_DATE(:StartDate, 'YYYY-MM-DD HH24:MI:SS') 
                                  AND TO_DATE(:EndDate, 'YYYY-MM-DD HH24:MI:SS')
                ) sub
                GROUP BY tester
                ORDER BY TOTAL DESC";

                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("StartDate", request.StartDate.ToString("yyyy-MM-dd HH:mm:ss")));
                        command.Parameters.Add(new OracleParameter("EndDate", request.EndDate.ToString("yyyy-MM-dd HH:mm:ss")));

                        var data = new List<dynamic>();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                data.Add(new
                                {
                                    ID = reader["ID"].ToString(),
                                    QTY_OK = Convert.ToInt32(reader["QTY_OK"]),
                                    QTY_NG = Convert.ToInt32(reader["QTY_NG"]),
                                    NOT_DEFINE = Convert.ToInt32(reader["NOT_DEFINE"]),
                                    TOTAL = Convert.ToInt32(reader["TOTAL"])
                                });
                            }
                        }

                        return Ok(new
                        {
                            success = true,
                            data
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        //API get-retest-detail-normal
        [HttpPost("get-retest-details-normal")]
        public async Task<IActionResult> GetRetestDetails([FromBody] TimeRequest request)
        {
            try
            {
                using (var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                SELECT 
                    date3,
                    tester,
                    serial_number,
                    model_name,
                    test_group,
                    data1,
                    NVL(REGEXP_SUBSTR(data19, 'KQ:([^/]+)', 1, 1, NULL, 1), 'not data') AS test_result,
                    NVL(REGEXP_SUBSTR(data19, 'remark:([^/]+)', 1, 1, NULL, 1), 'not data') AS remark
                FROM sfism4.r_repair_task_detail_t 
                WHERE data12 = 'RETEST'
                AND data17 = 'confirm'
                AND (data19 IS NULL OR data19 != 'CONFIRM_PUT_B36R')
                AND data1 NOT LIKE '%mcu_test_group%' 
                AND data1 NOT LIKE '%DPU_MEM_TEST%' 
                AND date3 BETWEEN TO_DATE(:StartDate, 'YYYY-MM-DD HH24:MI:SS') 
                              AND TO_DATE(:EndDate, 'YYYY-MM-DD HH24:MI:SS')
                ORDER BY tester DESC";

                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("StartDate", request.StartDate.ToString("yyyy-MM-dd HH:mm:ss")));
                        command.Parameters.Add(new OracleParameter("EndDate", request.EndDate.ToString("yyyy-MM-dd HH:mm:ss")));

                        var data = new List<dynamic>();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                data.Add(new
                                {
                                    Date = reader["date3"] != DBNull.Value ? Convert.ToDateTime(reader["date3"]) : (DateTime?)null,
                                    Tester = reader["tester"].ToString(),
                                    SerialNumber = reader["serial_number"].ToString(),
                                    ModelName = reader["model_name"].ToString(),
                                    TestGroup = reader["test_group"].ToString(),
                                    Data1 = reader["data1"].ToString(),
                                    TestResult = reader["test_result"].ToString(),
                                    Remark = reader["remark"].ToString()
                                });
                            }
                        }

                        return Ok(new
                        {
                            success = true,
                            data
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        //API get-retest-detail-MCU
        [HttpPost("get-retest-mcu-test-group")]
        public async Task<IActionResult> GetRetestMCUTestGroup([FromBody] TimeRequest request)
        {
            try
            {
                using (var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString))
                {
                    await connection.OpenAsync();
                    string query = @"
                        SELECT 
                            date3,
                            tester,
                            serial_number,
                            model_name,
                            test_group,
                            data1,
                            NVL(REGEXP_SUBSTR(data19, 'KQ:([^/]+)', 1, 1, NULL, 1), 'not data') AS test_result,
                            NVL(REGEXP_SUBSTR(data19, 'remark:([^/]+)', 1, 1, NULL, 1), 'not data') AS remark
                        FROM sfism4.r_repair_task_detail_t 
                        WHERE data12 = 'RETEST'
                        AND data17 = 'confirm'
                        AND (data19 IS NULL OR data19 != 'CONFIRM_PUT_B36R')
                        AND data1 LIKE '%mcu_test_group%' 
                        AND date3 BETWEEN TO_DATE(:StartDate, 'YYYY-MM-DD HH24:MI:SS') 
                                      AND TO_DATE(:EndDate, 'YYYY-MM-DD HH24:MI:SS')
                        ORDER BY tester DESC";

                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("StartDate", request.StartDate.ToString("yyyy-MM-dd HH:mm:ss")));
                        command.Parameters.Add(new OracleParameter("EndDate", request.EndDate.ToString("yyyy-MM-dd HH:mm:ss")));

                        var data = new List<dynamic>();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                data.Add(new
                                {
                                    Date = reader["date3"] != DBNull.Value ? Convert.ToDateTime(reader["date3"]) : (DateTime?)null,
                                    Tester = reader["tester"].ToString(),
                                    SerialNumber = reader["serial_number"].ToString(),
                                    ModelName = reader["model_name"].ToString(),
                                    TestGroup = reader["test_group"].ToString(),
                                    Data1 = reader["data1"].ToString(),
                                    TestResult = reader["test_result"].ToString(),
                                    Remark = reader["remark"].ToString()
                                });
                            }
                        }

                        return Ok(new
                        {
                            success = true,
                            data
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        //API get-retest-dpu-mem-test
        [HttpPost("get-retest-dpu-mem-test")]
        public async Task<IActionResult> GetRetestDPUMemTest([FromBody] TimeRequest request)
        {
            try
            {
                using (var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                SELECT 
                    date3,
                    tester,
                    serial_number,
                    model_name,
                    test_group,
                    data1,
                    NVL(REGEXP_SUBSTR(data19, 'KQ:([^/]+)', 1, 1, NULL, 1), 'not data') AS test_result,
                    NVL(REGEXP_SUBSTR(data19, 'remark:([^/]+)', 1, 1, NULL, 1), 'not data') AS remark
                FROM sfism4.r_repair_task_detail_t 
                WHERE data12 = 'RETEST'
                AND data17 = 'confirm'
                AND (data19 IS NULL OR data19 != 'CONFIRM_PUT_B36R')
                AND data1 LIKE '%DPU_MEM_TEST%' 
                AND date3 BETWEEN TO_DATE(:StartDate, 'YYYY-MM-DD HH24:MI:SS') 
                              AND TO_DATE(:EndDate, 'YYYY-MM-DD HH24:MI:SS')
                ORDER BY tester DESC";

                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("StartDate", request.StartDate.ToString("yyyy-MM-dd HH:mm:ss")));
                        command.Parameters.Add(new OracleParameter("EndDate", request.EndDate.ToString("yyyy-MM-dd HH:mm:ss")));

                        var data = new List<dynamic>();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                data.Add(new
                                {
                                    Date = reader["date3"] != DBNull.Value ? Convert.ToDateTime(reader["date3"]) : (DateTime?)null,
                                    Tester = reader["tester"].ToString(),
                                    SerialNumber = reader["serial_number"].ToString(),
                                    ModelName = reader["model_name"].ToString(),
                                    TestGroup = reader["test_group"].ToString(),
                                    Data1 = reader["data1"].ToString(),
                                    TestResult = reader["test_result"].ToString(),
                                    Remark = reader["remark"].ToString()
                                });
                            }
                        }

                        return Ok(new
                        {
                            success = true,
                            data
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        //[HttpGet("get-error-codes")]
        //public async Task<IActionResult> GetErrorCodes()
        //{
        //    try
        //    {
        //        // Sử dụng truy vấn SQL thô
        //        var sqlQuery = "SELECT ERROR_CODE, NVL(ERROR_DESC, '') AS ERROR_DESC FROM SFIS1.C_ERROR_CODE_T WHERE ROWNUM <= 1000";
        //        var errorCodes = await _oracleContext.Set<ErrorCode>()
        //            .FromSqlRaw(sqlQuery)
        //            .ToListAsync();

        //        // Log số lượng bản ghi để debug
        //        Console.WriteLine($"Số lượng mã lỗi được lấy: {errorCodes.Count}");

        //        return Ok(new
        //        {
        //            success = true,
        //            errorCodes = errorCodes.Select(e => new
        //            {
        //                e.ERROR_CODE,
        //                ERROR_DESC = e.ERROR_DESC
        //            })
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Lỗi khi lấy mã lỗi: {ex}");
        //        return StatusCode(500, new
        //        {
        //            success = false,
        //            message = "Lỗi khi lấy danh sách Error Codes",
        //            error = ex.Message,
        //            stackTrace = ex.StackTrace
        //        });
        //    }
        //}

        [HttpGet("get-error-codes")]
        public async Task<IActionResult> GetErrorCodes([FromQuery] string term = "", [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var query = _oracleContext.Set<ErrorCode>().FromSqlRaw("SELECT ERROR_CODE, ERROR_DESC FROM SFIS1.C_ERROR_CODE_T");

                // Tìm kiếm gần đúng nếu có term
                if (!string.IsNullOrEmpty(term))
                {
                    query = (IQueryable<ErrorCode>)query.Where(e =>
                        EF.Functions.Like(e.ERROR_CODE, $"%{term}%")
                    );
                }

                var totalCount = await query.CountAsync();
                var errorCodes = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                Console.WriteLine($"Số lượng mã lỗi được lấy: {errorCodes.Count}");

                return Ok(new
                {
                    success = true,
                    errorCodes = errorCodes.Select(e => new
                    {
                        e.ERROR_CODE,
                        ERROR_DESC = e.ERROR_DESC
                    }),
                    totalCount,
                    page,
                    pageSize
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi lấy mã lỗi: {ex}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách Error Codes",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }
    }
}