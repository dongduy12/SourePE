using API_WEB.ModelsDB;
using API_WEB.ModelsOracle;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace API_WEB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SAPController : ControllerBase
    {
        private readonly CSDL_NE _sqlContext;
        private readonly OracleDbContext _oracleDbContext;
        private readonly ILogger<SAPController> _logger;
        private readonly string _oracleConnectionString;
        private readonly string _sqlServerConnectionString;
        private const int MAX_SERIAL_NUMBERS = 2000; // Giới hạn số lượng SN trong IN clause

        public SAPController(
            CSDL_NE sqlContext,
            OracleDbContext oracleDbContext,
            ILogger<SAPController> logger,
            IConfiguration configuration)
        {
            _sqlContext = sqlContext ?? throw new ArgumentNullException(nameof(sqlContext));
            _oracleDbContext = oracleDbContext ?? throw new ArgumentNullException(nameof(oracleDbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _oracleConnectionString = configuration.GetConnectionString("OracleConnection")
                ?? throw new ArgumentNullException("OracleConnection string is missing");
            _sqlServerConnectionString = configuration.GetConnectionString("SqlServerConnection")
                ?? throw new ArgumentNullException("SqlServerConnection string is missing");
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ConsolidatedData>>> GetConsolidatedData(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            if (page < 1 || pageSize < 1)
            {
                return BadRequest("Invalid page or pageSize parameters");
            }

            try
            {
                _logger.LogInformation("Starting to fetch consolidated data. Page: {Page}, PageSize: {PageSize}", page, pageSize);

                // Lấy dữ liệu từ Oracle và SQL Server song song
                var oracleTask = GetOracleDataAsync();
                var sqlServerTask = GetSqlServerDataAsync();
                await Task.WhenAll(oracleTask, sqlServerTask);

                var oracleData = await oracleTask;
                var sqlServerData = await sqlServerTask;

                // Tổng hợp dữ liệu
                var consolidatedData = await ConsolidateDataAsync(oracleData, sqlServerData);

                // Phân trang
                var pagedData = consolidatedData
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return Ok(new
                {
                    TotalCount = consolidatedData.Count,
                    Page = page,
                    PageSize = pageSize,
                    Data = pagedData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching consolidated data");
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        private async Task<DataTable> GetOracleDataAsync()
        {
            var dt = new DataTable();
            try
            {
                using var conn = new OracleConnection(_oracleConnectionString);
                await conn.OpenAsync();

                const string query = @"
                SELECT 
                    CASE 
                        WHEN ((t.MODEL_NAME LIKE '900%') OR (t.MODEL_NAME LIKE '692%') OR (t.MODEL_NAME LIKE '930%')) AND kpt.KEY_PART_SN IS NOT NULL THEN kpt.KEY_PART_SN
                        ELSE t.SERIAL_NUMBER
                    END AS SERIAL_NUMBER,
                    t.MODEL_NAME,
                    t.MO_NUMBER,
                    t.TEST_GROUP,
                    t.TEST_CODE,
                    t.DATA1,
                    t.DATA11,
                    t.DATA12,
                    t.DATE3,
                    t.DATA13,
                    t.DATA18,
                    t.DATA19,
                    t.REASON_CODE,
                    w.WIP_GROUP,
                    w.WORK_FLAG,
                    CASE 
                        WHEN t.MODEL_NAME LIKE '900%' THEN 'After'
                        WHEN t.MODEL_NAME LIKE '930%' THEN 'After'
                        WHEN t.MODEL_NAME LIKE '692%' THEN 'After'
                        WHEN k.WIP_GROUP IS NOT NULL THEN 'After'
                        ELSE 'Before'
                    END AS KANBAN_WIP,
                    NVL(m2.PRODUCT_LINE, 'N/A') AS PRODUCT_LINE
                FROM SFISM4.R_REPAIR_TASK_T t
                INNER JOIN SFIS1.C_MODEL_DESC_T m ON t.MODEL_NAME = m.MODEL_NAME
                INNER JOIN SFISM4.R107 w ON t.SERIAL_NUMBER = w.SERIAL_NUMBER
                LEFT JOIN SFISM4.Z_KANBAN_TRACKING_T k ON t.SERIAL_NUMBER = k.SERIAL_NUMBER
                LEFT JOIN SFIS1.C_MODEL_DESC_T m2 ON t.MODEL_NAME = m2.MODEL_NAME
                LEFT JOIN (
                    SELECT 
                        KEY_PART_SN, 
                        SERIAL_NUMBER AS FG_SN -- SERIAL_NUMBER ở FG để join lại
                    FROM SFISM4.R_WIP_KEYPARTS_T
                    WHERE GROUP_NAME in ('SFG_LINK_FG', 'KANBAN_OUT')
                      AND KEY_PART_NO NOT LIKE '900%'
                      AND KEY_PART_NO NOT LIKE '692%'
                      AND KEY_PART_NO NOT LIKE '930%'
                ) kpt
                    ON t.SERIAL_NUMBER = kpt.FG_SN
                WHERE m.MODEL_SERIAL != 'SWITCH'";

                using var cmd = new OracleCommand(query, conn);
                using var adapter = new OracleDataAdapter(cmd);
                await Task.Run(() => adapter.Fill(dt));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Oracle data");
                throw;
            }
            return dt;
        }

        private async Task<DataTable> GetSqlServerDataAsync()
        {
            var dt = new DataTable();
            try
            {
                using var conn = new SqlConnection(_sqlServerConnectionString);
                await conn.OpenAsync();

                const string query = @"
                SELECT 
                    p.SerialNumber as SERIAL_NUMBER, 
                    s.ShelfCode, 
                    p.ColumnNumber, 
                    p.LevelNumber, 
                    p.TrayNumber, 
                    p.PositionInTray, 
                    p.EntryDate, 
                    p.EntryPerson, 
                    p.BorrowDate, 
                    p.BorrowPerson, 
                    p.BorrowStatus, 
                    p.Note, 
                    p.Action, 
                    CASE
						WHEN c.ApplyTaskStatus = 2 THEN 'Waiting SPE approve scrap'
						WHEN c.ApplyTaskStatus in (0, 1) THEN 'SPE approved scrap'
						WHEN c.ApplyTaskStatus = 3 THEN 'Waiting SPE approve to BGA'
						ELSE 'N/A'
					END AS ScrapStatus
                FROM 
                    Product p
                LEFT JOIN 
                    shelf s ON p.ShelfID = s.ShelfID
				LEFT JOIN
					ScrapList c ON p.SerialNumber = c.SN";

                using var cmd = new SqlCommand(query, conn);
                using var adapter = new SqlDataAdapter(cmd);
                await Task.Run(() => adapter.Fill(dt));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching SQL Server data");
                throw;
            }
            return dt;
        }

        private async Task<List<ConsolidatedData>> ConsolidateDataAsync(DataTable oracleData, DataTable sqlServerData)
        {
            var consolidatedData = new List<ConsolidatedData>();
            // Lấy danh sách SN từ Oracle không có trong Product
            var oracleOnlySerialNumbers = oracleData.AsEnumerable()
                .Select(row => row.Field<string>("SERIAL_NUMBER"))
                .Except(sqlServerData.AsEnumerable().Select(row => row.Field<string>("SERIAL_NUMBER")))
                .ToList();
            // Truy vấn ScrapList cho các SN từ Oracle không có trong Product
            Dictionary<string, string> scrapStatusDict = new Dictionary<string, string>();
            if (oracleOnlySerialNumbers.Any())
            {
                var scrapData = await GetScrapDataFromSqlServerAsync(oracleOnlySerialNumbers);
                scrapStatusDict = scrapData.AsEnumerable()
                    .ToDictionary(
                        row => row.Field<string>("SERIAL_NUMBER"),
                        row => row.Field<string>("ScrapStatus"));
            }

            // FULL OUTER JOIN cho dữ liệu có trong cả Oracle và SQL Server
            var mainJoin = from oracleRow in oracleData.AsEnumerable()
                           join sqlRow in sqlServerData.AsEnumerable()
                           on oracleRow.Field<string>("SERIAL_NUMBER") equals sqlRow.Field<string>("SERIAL_NUMBER") into gj
                           from sqlRow in gj.DefaultIfEmpty()
                           let serialNumber = oracleRow.Field<string>("SERIAL_NUMBER")
                           select new ConsolidatedData
                           {
                               SerialNumber = oracleRow.Field<string>("SERIAL_NUMBER") ?? string.Empty,
                               MoNumber = oracleRow.Field<string>("MO_NUMBER"),
                               ModelName = oracleRow.Field<string>("MODEL_NAME"),
                               TestGroup = oracleRow.Field<string>("TEST_GROUP"),
                               TestCode = oracleRow.Field<string>("TEST_CODE"),
                               Data1 = oracleRow.Field<string>("DATA1"),
                               Data11 = oracleRow.Field<string>("DATA11"),
                               Data12 = oracleRow.Field<string>("DATA12"),
                               Date3 = oracleRow.IsNull("DATE3") ? null : oracleRow.Field<DateTime>("DATE3"),
                               Data18 = oracleRow.Field<string>("DATA18"),
                               Data19 = oracleRow.Field<string>("DATA19"),
                               ReasonCode = oracleRow.Field<string>("REASON_CODE"),
                               WipGroup = oracleRow.Field<string>("WIP_GROUP") ?? string.Empty,
                               WorkFlag = oracleRow.Field<string>("WORK_FLAG") ?? string.Empty,
                               KanbanWip = oracleRow.Field<string>("KANBAN_WIP") ?? string.Empty,
                               ProductLine = oracleRow.Field<string>("PRODUCT_LINE") ?? string.Empty,
                               ShelfCode = sqlRow?.Field<string>("ShelfCode"),
                               ColumnNumber = sqlRow?.IsNull("ColumnNumber") == false ? sqlRow.Field<int>("ColumnNumber") : null,
                               LevelNumber = sqlRow?.IsNull("LevelNumber") == false ? sqlRow.Field<int>("LevelNumber") : null,
                               TrayNumber = sqlRow?.IsNull("TrayNumber") == false ? sqlRow.Field<int>("TrayNumber") : null,
                               PositionInTray = sqlRow?.IsNull("PositionInTray") == false ? sqlRow.Field<int>("PositionInTray") : null,
                               EntryDate = sqlRow?.IsNull("EntryDate") == false ? sqlRow.Field<DateTime>("EntryDate") : null,
                               EntryPerson = sqlRow?.Field<string>("EntryPerson"),
                               BorrowDate = sqlRow?.IsNull("BorrowDate") == false ? sqlRow.Field<DateTime>("BorrowDate") : null,
                               BorrowPerson = sqlRow?.Field<string>("BorrowPerson"),
                               BorrowStatus = sqlRow?.Field<string>("BorrowStatus"),
                               Note = sqlRow?.Field<string>("Note"),
                               ActionNe = sqlRow?.Field<string>("Action"),
                               ScrapStatus = sqlRow != null ? sqlRow.Field<string>("ScrapStatus") : (scrapStatusDict.ContainsKey(serialNumber) ? scrapStatusDict[serialNumber] : "N/A")
                           };

            consolidatedData.AddRange(mainJoin);

            // Xử lý các serial number chỉ có trong SQL Server
            var sqlOnlySerialNumbers = sqlServerData.AsEnumerable()
                .Select(row => row.Field<string>("SERIAL_NUMBER"))
                .Except(oracleData.AsEnumerable().Select(row => row.Field<string>("SERIAL_NUMBER")))
                .ToList();

            if (sqlOnlySerialNumbers.Any())
            {
                var productOldData = await GetProductOldFromSqlServerAsync(sqlOnlySerialNumbers);

                var sqlOnlyData = from sqlRow in sqlServerData.AsEnumerable()
                                  where sqlOnlySerialNumbers.Contains(sqlRow.Field<string>("SERIAL_NUMBER"))
                                  join productOldRow in productOldData.AsEnumerable()
                                  on sqlRow.Field<string>("SERIAL_NUMBER") equals productOldRow.Field<string>("SERIAL_NUMBER") into gj
                                  from productOldRow in gj.DefaultIfEmpty()
                                  select new ConsolidatedData
                                  {
                                      SerialNumber = sqlRow.Field<string>("SERIAL_NUMBER") ?? string.Empty,
                                      MoNumber = productOldRow?.Field<string>("MO_NUMBER"),
                                      ModelName = productOldRow?.Field<string>("MODEL_NAME"),
                                      TestGroup = productOldRow?.Field<string>("TEST_GROUP"),
                                      TestCode = productOldRow?.Field<string>("TEST_CODE"),
                                      ReasonCode = productOldRow?.Field<string>("REASON_CODE"),
                                      WipGroup = productOldRow?.Field<string>("WIP_GROUP") ?? string.Empty,
                                      WorkFlag = productOldRow?.Field<string>("WORK_FLAG") ?? string.Empty,
                                      KanbanWip = productOldRow?.Field<string>("KANBAN_WIP") ?? string.Empty,
                                      ProductLine = productOldRow?.Field<string>("PRODUCT_LINE") ?? string.Empty,
                                      Data1 = productOldRow?.Field<string>("ERROR_DESC"),
                                      ShelfCode = sqlRow.Field<string>("ShelfCode"),
                                      ColumnNumber = sqlRow.IsNull("ColumnNumber") ? null : sqlRow.Field<int>("ColumnNumber"),
                                      LevelNumber = sqlRow.IsNull("LevelNumber") ? null : sqlRow.Field<int>("LevelNumber"),
                                      TrayNumber = sqlRow.IsNull("TrayNumber") ? null : sqlRow.Field<int>("TrayNumber"),
                                      PositionInTray = sqlRow.IsNull("PositionInTray") ? null : sqlRow.Field<int>("PositionInTray"),
                                      EntryDate = sqlRow.IsNull("EntryDate") ? null : sqlRow.Field<DateTime>("EntryDate"),
                                      EntryPerson = sqlRow.Field<string>("EntryPerson"),
                                      BorrowDate = sqlRow.IsNull("BorrowDate") ? null : sqlRow.Field<DateTime>("BorrowDate"),
                                      BorrowPerson = sqlRow.Field<string>("BorrowPerson"),
                                      BorrowStatus = sqlRow.Field<string>("BorrowStatus"),
                                      Note = sqlRow.Field<string>("Note"),
                                      ActionNe = sqlRow.Field<string>("Action"),
                                      ScrapStatus = sqlRow.Field<string>("ScrapStatus")
                                  };
                consolidatedData.AddRange(sqlOnlyData);
            }
            return consolidatedData;
        }

        private async Task<DataTable> GetProductOldFromSqlServerAsync(List<string> serialNumbers)
        {
            var dt = new DataTable();
            try
            {
                using var conn = new SqlConnection(_sqlServerConnectionString);
                await conn.OpenAsync();

                var batches = serialNumbers
                    .Select((s, i) => new { Serial = s, Index = i })
                    .GroupBy(x => x.Index / 1000) // Batch size = 1000
                    .Select(g => g.Select(x => x.Serial).ToList());

                foreach (var batch in batches)
                {
                    var paramNames = new List<string>();
                    var parameters = batch.Select((s, i) => new SqlParameter($"@p{i}", SqlDbType.VarChar, 50) { Value = s }).ToArray();

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        paramNames.Add($"@p{i}");
                    }

                    var query = $@"SELECT *
                                   FROM ProductOld
                                   WHERE SERIAL_NUMBER IN ({string.Join(",", paramNames)})";

                    using var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddRange(parameters);
                    using var adapter = new SqlDataAdapter(cmd);
                    await Task.Run(() => adapter.Fill(dt));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ProductOld data from SQL Server");
                throw;
            }
            return dt;
        }

        private async Task<DataTable> GetScrapDataFromSqlServerAsync(List<string> serialNumbers)
        {
            var dt = new DataTable();
            try
            {
                using var conn = new SqlConnection(_sqlServerConnectionString);
                await conn.OpenAsync();

                var batches = serialNumbers
                    .Select((s, i) => new { Serial = s, Index = i })
                    .GroupBy(x => x.Index / 1000) // Kích thước batch = 100
                    .Select(g => g.Select(x => x.Serial).ToList());

                foreach (var batch in batches)
                {
                    var paramNames = new List<string>();
                    var parameters = batch.Select((s, i) => new SqlParameter($"@p{i}", SqlDbType.VarChar, 100) { Value = s }).ToArray();

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        paramNames.Add($"@p{i}");
                    }

                    var query = $@"SELECT 
                    c.SN as SERIAL_NUMBER,
                    CASE
                        WHEN c.ApplyTaskStatus = 2 THEN 'Waiting SPE approve scrap'
                        WHEN c.ApplyTaskStatus IN (0, 1) THEN 'SPE approved scrap'
                        WHEN c.ApplyTaskStatus = 3 THEN 'Waiting SPE approve to BGA'
                        ELSE 'N/A'
                    END AS ScrapStatus
                FROM 
                    ScrapList c
                WHERE 
                    c.SN IN ({string.Join(",", paramNames)})";

                    using var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddRange(parameters);
                    using var adapter = new SqlDataAdapter(cmd);
                    await Task.Run(() => adapter.Fill(dt));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy dữ liệu ScrapList từ SQL Server");
                throw;
            }
            return dt;
        }

    }
    public class ConsolidatedData
    {
        // From Oracle
        public string SerialNumber { get; set; } = string.Empty;
        public string? MoNumber { get; set; }
        public string? ModelName { get; set; }
        public string? TestGroup { get; set; }
        public string? TestCode { get; set; }
        public string? Data1 { get; set; } // ERROR_DESC
        public string? Data11 { get; set; } // STATUS_FA
        public string? Data12 { get; set; } // PRE_STATUS_FA
        public DateTime? Date3 { get; set; } // TIME
        public string? Tester { get; set; }
        public string? Data18 { get; set; } // Location
        public string? Data19 { get; set; } // Note FA
        public string? ReasonCode { get; set; }

        public string WipGroup { get; set; } = string.Empty;
        public string WorkFlag { get; set; } = string.Empty;
        public string KanbanWip { get; set; } = string.Empty;
        public string ProductLine { get; set; } = string.Empty;

        // From SQL Server
        public string? ShelfCode { get; set; }
        public int? ColumnNumber { get; set; }
        public int? LevelNumber { get; set; }
        public int? TrayNumber { get; set; }
        public int? PositionInTray { get; set; }
        public DateTime? EntryDate { get; set; }
        public string? EntryPerson { get; set; }
        public DateTime? BorrowDate { get; set; }
        public string? BorrowPerson { get; set; }
        public string? BorrowStatus { get; set; }
        public string? Note { get; set; }
        public string? ActionNe { get; set; }
        public string? ScrapStatus { get; set; }
    }
}