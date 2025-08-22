using API_WEB.Models.Repositories;
using API_WEB.Models.SmartFA;
using API_WEB.ModelsDB;
using API_WEB.ModelsOracle;
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Linq;

namespace API_WEB.Controllers.Repositories
{
    [Route("api/[controller]")]
    [ApiController]
    public class SearchController : ControllerBase
    {
        private readonly CSDL_NE _sqlContext;
        private readonly OracleDbContext _oracleContext;

        public SearchController(CSDL_NE sqlContext, OracleDbContext oracleContext)
        {
            _sqlContext = sqlContext;
            _oracleContext = oracleContext;
        }

        [HttpPost("GetKeyPartDetails")]
        public async Task<IActionResult> GetKeyPartDetails([FromBody] List<string> keyPartSNList)
        {
            if (keyPartSNList == null || !keyPartSNList.Any())
            {
                return BadRequest(new
                {
                    success = false,
                    message = "BGA QR CODE không được để trống."
                });
            }

            try
            {
                // Kết nối đến Oracle Database
                await using var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
                await connection.OpenAsync();

                var results = new List<object>();
                var notFoundSNs = new List<string>();

                foreach (var keyPartSN in keyPartSNList)
                {
                    var found = false;

                    string query = @"
                SELECT 
                    a.key_part_sn, 
                    a.serial_number, 
                    b.new_pn, 
                    b.model_name, 
                    b.TEST_CODE AS initial_test_code, 
                    b.test_group,
                    b.data1,
                    c.wip_group,
                    CASE 
                        WHEN EXISTS (
                            SELECT 1 
                            FROM sfism4.r117 d1
                            WHERE d1.serial_number = c.serial_number
                              AND d1.group_name = b.test_group
                              AND d1.error_flag = '0'
                        ) THEN 'PASS'
                        ELSE 'FAIL'
                    END AS STATUS
                FROM 
                    sfism4.r108 a
                INNER JOIN 
                    sfism4.r109 b
                ON a.serial_number = b.serial_number
                INNER JOIN
                    sfism4.r107 c
                ON b.serial_number = c.serial_number
                WHERE 
                    a.key_part_sn = :keyPartSN
                    AND b.new_pn IS NOT NULL
                    AND SUBSTR(b.new_pn, 1, 3) = 'MLX'
                    AND ROWNUM = 1";

                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("keyPartSN", keyPartSN));

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var status = reader["STATUS"].ToString();
                                var initialTestCode = reader["initial_test_code"].ToString();
                                var serialNumber = reader["serial_number"].ToString();
                                var testGroup = reader["test_group"].ToString();

                                if (status == "FAIL")
                                {
                                    string additionalQuery = @"
                                    SELECT TEST_CODE AS new_test_code
                                    FROM (
                                        SELECT TEST_CODE
                                        FROM sfism4.r109
                                        WHERE serial_number = :serialNumber
                                          AND SUBSTR(new_pn, 1, 3) = 'MLX'
                                          AND test_group = :testGroup
                                        ORDER BY test_time DESC
                                    )
                                    WHERE ROWNUM = 1";

                                    using (var additionalCommand = new OracleCommand(additionalQuery, connection))
                                    {
                                        additionalCommand.Parameters.Add(new OracleParameter("serialNumber", serialNumber));
                                        additionalCommand.Parameters.Add(new OracleParameter("testGroup", testGroup));

                                        using (var additionalReader = await additionalCommand.ExecuteReaderAsync())
                                        {
                                            if (await additionalReader.ReadAsync())
                                            {
                                                var newTestCode = additionalReader["new_test_code"].ToString();

                                                // Luôn thêm kết quả vào danh sách dù TEST_CODE cũ và mới có giống nhau hay không
                                                results.Add(new
                                                {
                                                    KeyPartSN = reader["key_part_sn"].ToString(),
                                                    SerialNumber = serialNumber,
                                                    NewPN = reader["new_pn"].ToString(),
                                                    ModelName = reader["model_name"].ToString(),
                                                    WIPGroup = reader["wip_group"].ToString(),
                                                    Data1 = reader["data1"].ToString(),
                                                    Status = status,
                                                    OldTestCode = initialTestCode,
                                                    NewTestCode = newTestCode
                                                });

                                                found = true; // Đánh dấu đã tìm thấy kết quả
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    results.Add(new
                                    {
                                        KeyPartSN = reader["key_part_sn"].ToString(),
                                        SerialNumber = serialNumber,
                                        NewPN = reader["new_pn"].ToString(),
                                        ModelName = reader["model_name"].ToString(),
                                        WIPGroup = reader["wip_group"].ToString(),
                                        Data1 = reader["data1"].ToString(),
                                        OldTestCode = initialTestCode,
                                        NewTestCode = "",
                                        Status = status
                                    });
                                    found = true;
                                }
                            }
                        }
                    }

                    if (!found)
                    {
                        notFoundSNs.Add(keyPartSN);
                    }
                }

                return Ok(new
                {
                    success = true,
                    totalFound = results.Count,
                    totalNotFound = notFoundSNs.Count,
                    data = results,
                    notFoundSNs = notFoundSNs
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Lỗi khi thực thi truy vấn: {ex.Message}"
                });
            }
        }


        [HttpPost("SearchProductsBySN")]
        public async Task<IActionResult> SearchProductsBySN([FromBody] List<string> serialNumbers)
        {
            try
            {
                if (serialNumbers == null || !serialNumbers.Any())
                {
                    return BadRequest(new { success = false, message = "Danh sách serialNumbers rỗng." });
                }

                var results = new List<InforProduct>();
                var notFoundSerialNumbers = new List<string>();

                // 1. Tìm tất cả serialNumbers trong SQL Server
                var productsFromSql = await _sqlContext.Products
                    .Include(p => p.Shelf)
                    .Where(p => serialNumbers.Contains(p.SerialNumber))
                    .ToListAsync();

                var foundSerialNumbersInSql = productsFromSql.Select(p => p.SerialNumber).ToList();

                // Xác định các serialNumbers chưa tìm thấy trong SQL Server
                var remainingSerialNumbers = serialNumbers.Except(foundSerialNumbersInSql).ToList();

                // 2. Kết nối Oracle
                await using var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
                await connection.OpenAsync();

                // 3. Lấy dữ liệu từ Oracle cho tất cả serialNumbers
                var oracleData = await GetOracleDataAsync(connection, serialNumbers);

                // 4. Kết hợp dữ liệu từ SQL Server và Oracle
                foreach (var product in productsFromSql)
                {
                    var oracleInfo = oracleData.GetValueOrDefault(product.SerialNumber);

                    results.Add(new InforProduct
                    {
                        SerialNumber = product.SerialNumber,
                        ProductLine = product?.ProductLine ?? "",
                        LevelNumber = product?.LevelNumber,
                        ShelfCode = product?.Shelf?.ShelfCode ?? "",
                        TrayNumber = product?.TrayNumber,
                        PositionInTray = product?.PositionInTray,
                        ColumnNumber = product?.ColumnNumber,
                        EntryDate = product?.EntryDate,
                        BorrowDate = product?.BorrowDate,
                        BorrowPerson = product?.BorrowPerson ?? "",
                        EntryPerson = product?.EntryPerson ?? "",
                        BorrowStatus = product?.BorrowStatus ?? "Available",
                        Note = product?.Note ?? "",
                        KanBanWIP = product?.KANBAN_WIP ?? "",
                        ActionNe = product?.Action ?? "",
                        Scrap = product?.Scrap ?? "",
                        MoNumber = oracleInfo?.MoNumber ?? "",
                        Data1 = oracleInfo?.Data1 ?? "",
                        ModelName = oracleInfo?.ModelName ?? "",
                        TestGroup = oracleInfo?.TestGroup ?? "",
                        TestCode = oracleInfo?.TestCode ?? "",
                        ReasonCode = oracleInfo?.ReasonCode ?? "",
                        WipGroup = oracleInfo?.WipGroup ?? "",
                        WorkFlag = oracleInfo?.WorkFlag ?? "",
                        BlockReason = oracleInfo?.BlockReason ?? "",
                        HoldReason = oracleInfo?.HoldReason ?? ""
                    });
                }

                // 5. Thêm các serialNumbers không tìm thấy vào danh sách notFoundSerialNumbers
                notFoundSerialNumbers = serialNumbers.Except(results.Select(r => r.SerialNumber)).ToList();

                // 6. Trả về kết quả
                return Ok(new
                {
                    success = true,
                    totalFound = results.Count,
                    totalNotFound = notFoundSerialNumbers.Count,
                    results,
                    notFoundSerialNumbers
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"System Error: {ex.Message}");
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        //=======================END SEARCH SN=====================

        [HttpPost("SearchSnOK")]
        public async Task<IActionResult> SearchSnOk([FromBody] List<string> serialNumbers)
        {
            try
            {
                if (serialNumbers == null || !serialNumbers.Any())
                {
                    return BadRequest(new { success = false, message = "Danh sách serialNumbers rỗng." });
                }

                var results = new List<InforProduct>();
                var notFoundSerialNumbers = new List<string>();

                // 1. Tìm tất cả serialNumbers trong SQL Server
                var productsFromSql = await _sqlContext.KhoOks
                    .Where(p => serialNumbers.Contains(p.SERIAL_NUMBER))
                    .ToListAsync();

                var foundSerialNumbersInSql = productsFromSql.Select(p => p.SERIAL_NUMBER).ToList();

                // Xác định các serialNumbers chưa tìm thấy trong SQL Server
                var remainingSerialNumbers = serialNumbers.Except(foundSerialNumbersInSql).ToList();

                // 2. Kết nối Oracle
                await using var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
                await connection.OpenAsync();

                // 3. Lấy dữ liệu từ Oracle cho tất cả serialNumbers
                var oracleData = await GetOracleDataAsync(connection, serialNumbers);

                // 4. Kết hợp dữ liệu từ SQL Server và Oracle
                foreach (var product in productsFromSql)
                {
                    var oracleInfo = oracleData.GetValueOrDefault(product.SERIAL_NUMBER);

                    results.Add(new InforProduct
                    {
                        SerialNumber = product.SERIAL_NUMBER,
                        LevelNumber = product?.LevelNumber,
                        ShelfCode = product?.ShelfCode ?? "",
                        ColumnNumber = product?.ColumnNumber,
                        EntryDate = product?.entryDate,
                        EntryPerson = product?.entryPerson ?? "",
                        Note = product?.Note ?? "",
                        ProductLine = oracleInfo?.ProductLine ?? "",
                        MoNumber = oracleInfo?.MoNumber ?? "",
                        Data1 = oracleInfo?.Data1 ?? "",
                        ModelName = oracleInfo?.ModelName ?? "",
                        TestGroup = oracleInfo?.TestGroup ?? "",
                        TestCode = oracleInfo?.TestCode ?? "",
                        ReasonCode = oracleInfo?.ReasonCode ?? "",
                        WipGroup = oracleInfo?.WipGroup ?? "",
                        WorkFlag = oracleInfo?.WorkFlag ?? "",
                        BorrowStatus = product?.borrowStatus ?? "",
                        BorrowDate = product?.borrowDate,
                        BorrowPerson = product?.borrowPerson ?? "",
                        BlockReason = oracleInfo?.BlockReason ?? ""
                    });
                }

                // 5. Thêm các serialNumbers không tìm thấy vào danh sách notFoundSerialNumbers
                notFoundSerialNumbers = serialNumbers.Except(results.Select(r => r.SerialNumber)).ToList();

                // 6. Trả về kết quả
                return Ok(new
                {
                    success = true,
                    totalFound = results.Count,
                    totalNotFound = notFoundSerialNumbers.Count,
                    results,
                    notFoundSerialNumbers
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"System Error: {ex.Message}");
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }
        //=====================XUẤT EXCEL KHO OKs==================
        [HttpGet("ExportExcelKhoOk")]
        public async Task<IActionResult> ExportExcelKhoOk()
        {
            try
            {
                var allProducts = await _sqlContext.KhoOks
                    .GroupJoin(_sqlContext.ScrapLists,
                        ks => ks.SERIAL_NUMBER,
                        sl => sl.SN,
                        (ks, slGroup) => new { KhoOk = ks, ScrapList = slGroup })
                    .SelectMany(
                        x => x.ScrapList.DefaultIfEmpty(),
                        (ks, sl) => new
                        {
                            ks.KhoOk.SERIAL_NUMBER,
                            ks.KhoOk.ShelfCode,
                            ks.KhoOk.ColumnNumber,
                            ks.KhoOk.LevelNumber,
                            ks.KhoOk.entryDate,
                            ks.KhoOk.entryPerson,
                            ks.KhoOk.borrowStatus,
                            ks.KhoOk.borrowDate,
                            ks.KhoOk.borrowPerson,
                            ks.KhoOk.Note,
                            TaskNumber = sl != null ? sl.TaskNumber : null
                        })
                    .ToListAsync();

                if (!allProducts.Any())
                {
                    return BadRequest(new { success = false, message = "Không có dữ liệu để xuất." });
                }

                // 2. Chuẩn bị danh sách serialNumbers
                var serialNumbers = allProducts.Select(p => p.SERIAL_NUMBER).ToList();

                // 3. Kết nối Oracle và lấy dữ liệu nhóm
                var excelData = new List<InforProduct>();
                await using var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
                await connection.OpenAsync();

                // Chia nhỏ serialNumbers thành các batch (tối đa 1000 phần tử mỗi batch)
                var batches = serialNumbers
                    .Select((value, index) => new { value, index })
                    .GroupBy(x => x.index / 1000)
                    .Select(group => group.Select(x => x.value).ToList())
                    .ToList();

                // Dữ liệu từ Oracle cho tất cả serialNumbers
                var oracleData = new Dictionary<string, InforProduct>();

                foreach (var batch in batches)
                {
                    var batchData = await GetOracleDataAsync(connection, batch);
                    foreach (var entry in batchData)
                    {
                        oracleData[entry.Key] = entry.Value;
                    }
                }

                // 4. Kết hợp dữ liệu từ SQL Server và Oracle
                foreach (var product in allProducts)
                {
                    var oracleInfo = oracleData.GetValueOrDefault(product.SERIAL_NUMBER);
                    var type = product.TaskNumber == null && !_sqlContext.ScrapLists.Any(sl => sl.SN == product.SERIAL_NUMBER)
                        ? "No_Scrap"
                        : product.TaskNumber != null
                            ? "Scrap_has_task"
                            : "Scrap_lacks_task";
                    excelData.Add(new InforProduct
                    {
                        SerialNumber = product.SERIAL_NUMBER,
                        ShelfCode = product?.ShelfCode ?? "",
                        ColumnNumber = product?.ColumnNumber,
                        LevelNumber = product?.LevelNumber,
                        EntryDate = product?.entryDate,
                        EntryPerson = product?.entryPerson ?? "",
                        BorrowStatus = product?.borrowStatus ?? "",
                        BorrowDate = product?.borrowDate,
                        BorrowPerson = product?.borrowPerson ?? "",
                        Note = product?.Note ?? "",
                        ProductLine = oracleInfo?.ProductLine ?? "",
                        BlockReason = oracleInfo?.BlockReason ?? "",
                        TestCode = oracleInfo?.TestCode ?? "",
                        Data1 = oracleInfo?.Data1 ?? "",
                        WipGroup = oracleInfo?.WipGroup ?? "",
                        WorkFlag = oracleInfo?.WorkFlag ?? "",
                        TestGroup = oracleInfo?.TestGroup ?? "",
                        MoNumber = oracleInfo?.MoNumber ?? "",
                        ModelName = oracleInfo?.ModelName ?? "",
                        ReasonCode = oracleInfo?.ReasonCode ?? "",
                        Type = type
                    });
                }

                // 5. Tạo file Excel
                using (var workbook = new ClosedXML.Excel.XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("All_Data");
                    var currentRow = 1;

                    // Tạo header
                    worksheet.Cell(currentRow, 1).Value = "SERIAL_NUMBER";
                    worksheet.Cell(currentRow, 2).Value = "PRODUCT_LINE";
                    worksheet.Cell(currentRow, 3).Value = "MODEL_NAME";
                    worksheet.Cell(currentRow, 4).Value = "MO_NUMBER";
                    worksheet.Cell(currentRow, 5).Value = "WIP_GROUP";
                    worksheet.Cell(currentRow, 6).Value = "WORK_FLAG";
                    worksheet.Cell(currentRow, 7).Value = "KỆ";
                    worksheet.Cell(currentRow, 8).Value = "CỘT";
                    worksheet.Cell(currentRow, 9).Value = "TẦNG";
                    worksheet.Cell(currentRow, 10).Value = "BLOCK_REASON";
                    worksheet.Cell(currentRow, 11).Value = "TEST_GROUP";
                    worksheet.Cell(currentRow, 12).Value = "TEST_CODE";
                    worksheet.Cell(currentRow, 13).Value = "ERROR_DESC";
                    worksheet.Cell(currentRow, 14).Value = "REASON_CODE";
                    worksheet.Cell(currentRow, 15).Value = "NGÀY_NHẬP";
                    worksheet.Cell(currentRow, 16).Value = "NGƯỜI_NHẬP";
                    worksheet.Cell(currentRow, 17).Value = "BORROW_STATUS";
                    worksheet.Cell(currentRow, 18).Value = "BORROW_DATE";
                    worksheet.Cell(currentRow, 19).Value = "BORROW_PERSON";
                    worksheet.Cell(currentRow, 20).Value = "NOTE";
                    worksheet.Cell(currentRow, 21).Value = "TYPE";
                    // Điền dữ liệu
                    foreach (var data in excelData)
                    {
                        currentRow++;
                        worksheet.Cell(currentRow, 1).Value = data.SerialNumber;
                        worksheet.Cell(currentRow, 2).Value = data.ProductLine;
                        worksheet.Cell(currentRow, 3).Value = data.ModelName;
                        worksheet.Cell(currentRow, 4).Value = data.MoNumber;
                        worksheet.Cell(currentRow, 5).Value = data.WipGroup;
                        worksheet.Cell(currentRow, 6).Value = data.WorkFlag;
                        worksheet.Cell(currentRow, 7).Value = data.ShelfCode;
                        worksheet.Cell(currentRow, 8).Value = data.ColumnNumber;
                        worksheet.Cell(currentRow, 9).Value = data.LevelNumber;
                        worksheet.Cell(currentRow, 10).Value = data.BlockReason;
                        worksheet.Cell(currentRow, 11).Value = data.TestGroup;
                        worksheet.Cell(currentRow, 12).Value = data.TestCode;
                        worksheet.Cell(currentRow, 13).Value = data.Data1;
                        worksheet.Cell(currentRow, 14).Value = data.ReasonCode;
                        worksheet.Cell(currentRow, 15).Value = data.EntryDate;
                        worksheet.Cell(currentRow, 16).Value = data.EntryPerson;
                        worksheet.Cell(currentRow, 17).Value = data.BorrowStatus;
                        worksheet.Cell(currentRow, 18).Value = data.BorrowDate;
                        worksheet.Cell(currentRow, 19).Value = data.BorrowPerson;
                        worksheet.Cell(currentRow, 20).Value = data.Note;
                        worksheet.Cell(currentRow, 21).Value = data.Type;
                    }

                    // Trả file Excel về client
                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "DataScrap.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"System Error: {ex.Message}");
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }


        [HttpPost("SearchSNScrap")]
        public async Task<IActionResult> SearchSNScrap([FromBody] List<string> serialNumbers)
        {
            try
            {
                if (serialNumbers == null || !serialNumbers.Any())
                {
                    return BadRequest(new { success = false, message = "Danh sách serialNumbers rỗng." });
                }

                var results = new List<InforProduct>();
                var notFoundSerialNumbers = new List<string>();

                // 1. Tìm tất cả serialNumbers trong SQL Server
                var productsFromSql = await _sqlContext.KhoScraps
                    .Where(p => serialNumbers.Contains(p.SERIAL_NUMBER))
                    .ToListAsync();

                var foundSerialNumbersInSql = productsFromSql.Select(p => p.SERIAL_NUMBER).ToList();

                // Xác định các serialNumbers chưa tìm thấy trong SQL Server
                var remainingSerialNumbers = serialNumbers.Except(foundSerialNumbersInSql).ToList();

                // 2. Kết nối Oracle
                await using var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
                await connection.OpenAsync();

                // 3. Lấy dữ liệu từ Oracle cho tất cả serialNumbers
                var oracleData = await GetOracleDataAsync(connection, serialNumbers);

                // 4. Kết hợp dữ liệu từ SQL Server và Oracle
                foreach (var product in productsFromSql)
                {
                    var oracleInfo = oracleData.GetValueOrDefault(product.SERIAL_NUMBER);

                    results.Add(new InforProduct
                    {
                        SerialNumber = product.SERIAL_NUMBER,
                        LevelNumber = product?.LevelNumber,
                        ShelfCode = product?.ShelfCode ?? "",
                        TrayNumber = product?.TrayNumber,
                        PositionInTray = product?.Position,
                        ColumnNumber = product?.ColumnNumber,
                        EntryDate = product?.entryDate,
                        EntryPerson = product?.entryPerson ?? "",
                        Note = product?.Note ?? "",
                        ProductLine = oracleInfo?.ProductLine?? "",
                        MoNumber = oracleInfo?.MoNumber ?? "",
                        Data1 = oracleInfo?.Data1 ?? "",
                        ModelName = oracleInfo?.ModelName ?? "",
                        TestGroup = oracleInfo?.TestGroup ?? "",
                        TestCode = oracleInfo?.TestCode ?? "",
                        ReasonCode = oracleInfo?.ReasonCode ?? "",
                        WipGroup = oracleInfo?.WipGroup ?? "",
                        WorkFlag = oracleInfo?.WorkFlag ?? "",
                        BorrowStatus = product?.borrowStatus ?? "",
                        BorrowDate = product?.borrowDate,
                        BorrowPerson = product?.borrowPerson ?? "",
                        BlockReason = oracleInfo?.BlockReason ?? ""
                    });
                }

                // 5. Thêm các serialNumbers không tìm thấy vào danh sách notFoundSerialNumbers
                notFoundSerialNumbers = serialNumbers.Except(results.Select(r => r.SerialNumber)).ToList();

                // 6. Trả về kết quả
                return Ok(new
                {
                    success = true,
                    totalFound = results.Count,
                    totalNotFound = notFoundSerialNumbers.Count,
                    results,
                    notFoundSerialNumbers
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"System Error: {ex.Message}");
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        [HttpGet("FindLocation")]
        public async Task<IActionResult> FindLocation([FromQuery] string serialNumber)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                return BadRequest(new { success = false, message = "serialNumber is required." });
            }

            serialNumber = serialNumber.Trim().ToUpper();

            try
            {
                var product = await _sqlContext.Products
                    .Include(p => p.Shelf)
                    .FirstOrDefaultAsync(p => p.SerialNumber == serialNumber);

                var khoOk = product == null
                    ? await _sqlContext.KhoOks.FirstOrDefaultAsync(p => p.SERIAL_NUMBER == serialNumber)
                    : null;

                var khoScrap = product == null && khoOk == null
                    ? await _sqlContext.KhoScraps.FirstOrDefaultAsync(p => p.SERIAL_NUMBER == serialNumber)
                    : null;

                if (product == null && khoOk == null && khoScrap == null)
                {
                    return Ok(new { success = false, message = "Serial number not found." });
                }

                await using var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
                await connection.OpenAsync();
                var oracleData = await GetOracleDataAsync(connection, new List<string> { serialNumber });
                var oracleInfo = oracleData.GetValueOrDefault(serialNumber);

                object result;

                if (product != null)
                {
                    result = new
                    {
                        warehouse = "Product",
                        serialNumber = product.SerialNumber,
                        shelfCode = product.Shelf?.ShelfCode ?? "",
                        columnNumber = product.ColumnNumber,
                        levelNumber = product.LevelNumber,
                        trayNumber = product.TrayNumber,
                        positionInTray = product.PositionInTray,
                        productLine = oracleInfo?.ProductLine ?? "",
                        modelName = oracleInfo?.ModelName ?? ""
                    };
                }
                else if (khoOk != null)
                {
                    result = new
                    {
                        warehouse = "KhoOk",
                        serialNumber = khoOk.SERIAL_NUMBER,
                        shelfCode = khoOk.ShelfCode ?? "",
                        columnNumber = khoOk.ColumnNumber,
                        levelNumber = khoOk.LevelNumber,
                        trayNumber = (int?)null,
                        positionInTray = (int?)null,
                        productLine = oracleInfo?.ProductLine ?? "",
                        modelName = oracleInfo?.ModelName ?? ""
                    };
                }
                else
                {
                    result = new
                    {
                        warehouse = "KhoScrap",
                        serialNumber = khoScrap!.SERIAL_NUMBER,
                        shelfCode = khoScrap.ShelfCode ?? "",
                        columnNumber = khoScrap.ColumnNumber,
                        levelNumber = khoScrap.LevelNumber,
                        trayNumber = khoScrap.TrayNumber,
                        positionInTray = khoScrap.Position,
                        productLine = oracleInfo?.ProductLine ?? "",
                        modelName = oracleInfo?.ModelName ?? ""
                    };
                }

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        [HttpPost("FindLocations")]
        public async Task<IActionResult> FindLocations([FromBody] List<string> serialNumbers)
        {
            if (serialNumbers == null || !serialNumbers.Any())
            {
                return BadRequest(new { success = false, message = "serialNumbers is required." });
            }

            var sns = serialNumbers
                .Where(sn => !string.IsNullOrWhiteSpace(sn))
                .Select(sn => sn.Trim().ToUpper())
                .Distinct()
                .ToList();

            try
            {
                var results = new List<object>();

                var products = await _sqlContext.Products
                    .Include(p => p.Shelf)
                    .Where(p => sns.Contains(p.SerialNumber))
                    .ToListAsync();

                var foundProducts = products.Select(p => p.SerialNumber).ToHashSet();
                var remaining = sns.Except(foundProducts).ToList();

                var khoOks = await _sqlContext.KhoOks
                    .Where(k => remaining.Contains(k.SERIAL_NUMBER))
                    .ToListAsync();

                var foundOks = khoOks.Select(k => k.SERIAL_NUMBER).ToHashSet();
                remaining = remaining.Except(foundOks).ToList();

                var khoScraps = await _sqlContext.KhoScraps
                    .Where(k => remaining.Contains(k.SERIAL_NUMBER))
                    .ToListAsync();

                var foundScraps = khoScraps.Select(k => k.SERIAL_NUMBER).ToHashSet();

                await using var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
                await connection.OpenAsync();
                var oracleData = await GetOracleDataAsync(connection, sns);

                foreach (var p in products)
                {
                    var info = oracleData.GetValueOrDefault(p.SerialNumber);
                    results.Add(new
                    {
                        serialNumber = p.SerialNumber,
                        warehouse = "Product",
                        shelfCode = p.Shelf?.ShelfCode ?? "",
                        columnNumber = p.ColumnNumber,
                        levelNumber = p.LevelNumber,
                        trayNumber = p.TrayNumber,
                        positionInTray = p.PositionInTray,
                        productLine = info?.ProductLine ?? "",
                        modelName = info?.ModelName ?? ""
                    });
                }

                foreach (var k in khoOks)
                {
                    var info = oracleData.GetValueOrDefault(k.SERIAL_NUMBER);
                    results.Add(new
                    {
                        serialNumber = k.SERIAL_NUMBER,
                        warehouse = "KhoOk",
                        shelfCode = k.ShelfCode ?? "",
                        columnNumber = k.ColumnNumber,
                        levelNumber = k.LevelNumber,
                        trayNumber = (int?)null,
                        positionInTray = (int?)null,
                        productLine = info?.ProductLine ?? "",
                        modelName = info?.ModelName ?? ""
                    });
                }

                foreach (var s in khoScraps)
                {
                    var info = oracleData.GetValueOrDefault(s.SERIAL_NUMBER);
                    results.Add(new
                    {
                        serialNumber = s.SERIAL_NUMBER,
                        warehouse = "KhoScrap",
                        shelfCode = s.ShelfCode ?? "",
                        columnNumber = s.ColumnNumber,
                        levelNumber = s.LevelNumber,
                        trayNumber = s.TrayNumber,
                        positionInTray = s.Position,
                        productLine = info?.ProductLine ?? "",
                        modelName = info?.ModelName ?? ""
                    });
                }

                var foundSet = foundProducts.Union(foundOks).Union(foundScraps);
                var notFound = sns.Except(foundSet).ToList();

                return Ok(new
                {
                    success = true,
                    data = results,
                    totalFound = results.Count,
                    totalNotFound = notFound.Count,
                    notFoundSerialNumbers = notFound
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        //=======================XUẤT EXCEL SCRAP====================
        [HttpGet("ExportExcelScrap")]
        public async Task<IActionResult> ExportExcelScrap()
        {
            try
            {
                var allProducts = await _sqlContext.KhoScraps
                    .GroupJoin(_sqlContext.ScrapLists,
                        ks => ks.SERIAL_NUMBER,
                        sl => sl.SN,
                        (ks, slGroup) => new { KhoScrap = ks, ScrapList = slGroup })
                    .SelectMany(
                        x => x.ScrapList.DefaultIfEmpty(),
                        (ks, sl) => new
                        {
                            ks.KhoScrap.SERIAL_NUMBER,
                            ks.KhoScrap.ShelfCode,
                            ks.KhoScrap.ColumnNumber,
                            ks.KhoScrap.LevelNumber,
                            ks.KhoScrap.TrayNumber,
                            ks.KhoScrap.Position,
                            ks.KhoScrap.entryDate,
                            ks.KhoScrap.entryPerson,
                            ks.KhoScrap.borrowStatus,
                            ks.KhoScrap.borrowDate,
                            ks.KhoScrap.borrowPerson,
                            ks.KhoScrap.Note,
                            TaskNumber = sl != null ? sl.TaskNumber : null
                        })
                    .ToListAsync();

                if (!allProducts.Any())
                {
                    return BadRequest(new { success = false, message = "Không có dữ liệu để xuất." });
                }

                // 2. Chuẩn bị danh sách serialNumbers
                var serialNumbers = allProducts.Select(p => p.SERIAL_NUMBER).ToList();

                // 3. Kết nối Oracle và lấy dữ liệu nhóm
                var excelData = new List<InforProduct>();
                await using var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
                await connection.OpenAsync();

                // Chia nhỏ serialNumbers thành các batch (tối đa 1000 phần tử mỗi batch)
                var batches = serialNumbers
                    .Select((value, index) => new { value, index })
                    .GroupBy(x => x.index / 1000)
                    .Select(group => group.Select(x => x.value).ToList())
                    .ToList();

                // Dữ liệu từ Oracle cho tất cả serialNumbers
                var oracleData = new Dictionary<string, InforProduct>();

                foreach (var batch in batches)
                {
                    var batchData = await GetOracleDataAsync(connection, batch);
                    foreach (var entry in batchData)
                    {
                        oracleData[entry.Key] = entry.Value;
                    }
                }

                // 4. Kết hợp dữ liệu từ SQL Server và Oracle
                foreach (var product in allProducts)
                {
                    var oracleInfo = oracleData.GetValueOrDefault(product.SERIAL_NUMBER);
                    var type = product.TaskNumber == null && !_sqlContext.ScrapLists.Any(sl => sl.SN == product.SERIAL_NUMBER)
                        ? "No_Scrap"
                        : product.TaskNumber != null
                            ? "Scrap_has_task"
                            : "Scrap_lacks_task";
                    excelData.Add(new InforProduct
                    {
                        SerialNumber = product.SERIAL_NUMBER,
                        ShelfCode = product?.ShelfCode ?? "",
                        ColumnNumber = product?.ColumnNumber,
                        LevelNumber = product?.LevelNumber,
                        TrayNumber = product?.TrayNumber,
                        PositionInTray = product?.Position,
                        EntryDate = product?.entryDate,
                        EntryPerson = product?.entryPerson ?? "",
                        BorrowStatus = product?.borrowStatus ?? "",
                        BorrowDate = product?.borrowDate,
                        BorrowPerson = product?.borrowPerson ?? "",
                        Note = product?.Note ?? "",
                        ProductLine = oracleInfo?.ProductLine ?? "",
                        BlockReason = oracleInfo?.BlockReason ?? "",
                        TestCode = oracleInfo?.TestCode ?? "",
                        Data1 = oracleInfo?.Data1 ?? "",
                        WipGroup = oracleInfo?.WipGroup ?? "",
                        WorkFlag = oracleInfo?.WorkFlag ?? "",
                        TestGroup = oracleInfo?.TestGroup ?? "",
                        MoNumber = oracleInfo?.MoNumber ?? "",
                        ModelName = oracleInfo?.ModelName ?? "",
                        ReasonCode = oracleInfo?.ReasonCode ?? "",
                        Type = type
                    });
                }

                // 5. Tạo file Excel
                using (var workbook = new ClosedXML.Excel.XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("All_Data");
                    var currentRow = 1;

                    // Tạo header
                    worksheet.Cell(currentRow, 1).Value = "SERIAL_NUMBER";
                    worksheet.Cell(currentRow, 2).Value = "PRODUCT_LINE";
                    worksheet.Cell(currentRow, 3).Value = "MODEL_NAME";
                    worksheet.Cell(currentRow, 4).Value = "MO_NUMBER";
                    worksheet.Cell(currentRow, 5).Value = "WIP_GROUP";
                    worksheet.Cell(currentRow, 6).Value = "WORK_FLAG";
                    worksheet.Cell(currentRow, 7).Value = "KỆ";
                    worksheet.Cell(currentRow, 8).Value = "CỘT";
                    worksheet.Cell(currentRow, 9).Value = "TẦNG";
                    worksheet.Cell(currentRow, 10).Value = "KHAY";
                    worksheet.Cell(currentRow, 11).Value = "Ô";
                    worksheet.Cell(currentRow, 12).Value = "BLOCK_REASON";
                    worksheet.Cell(currentRow, 13).Value = "TEST_GROUP";
                    worksheet.Cell(currentRow, 14).Value = "TEST_CODE";
                    worksheet.Cell(currentRow, 15).Value = "ERROR_DESC";
                    worksheet.Cell(currentRow, 16).Value = "REASON_CODE";
                    worksheet.Cell(currentRow, 17).Value = "NGÀY_NHẬP";
                    worksheet.Cell(currentRow, 18).Value = "NGƯỜI_NHẬP";
                    worksheet.Cell(currentRow, 19).Value = "BORROW_STATUS";
                    worksheet.Cell(currentRow, 20).Value = "BORROW_DATE";
                    worksheet.Cell(currentRow, 21).Value = "BORROW_PERSON";
                    worksheet.Cell(currentRow, 22).Value = "NOTE";
                    worksheet.Cell(currentRow, 23).Value = "TYPE";
                    // Điền dữ liệu
                    foreach (var data in excelData) 
                    {
                        currentRow++;
                        worksheet.Cell(currentRow, 1).Value = data.SerialNumber;
                        worksheet.Cell(currentRow, 2).Value = data.ProductLine;
                        worksheet.Cell(currentRow, 3).Value = data.ModelName;
                        worksheet.Cell(currentRow, 4).Value = data.MoNumber;
                        worksheet.Cell(currentRow, 5).Value = data.WipGroup;
                        worksheet.Cell(currentRow, 6).Value = data.WorkFlag;
                        worksheet.Cell(currentRow, 7).Value = data.ShelfCode;
                        worksheet.Cell(currentRow, 8).Value = data.ColumnNumber;
                        worksheet.Cell(currentRow, 9).Value = data.LevelNumber;
                        worksheet.Cell(currentRow, 10).Value = data.TrayNumber;
                        worksheet.Cell(currentRow, 11).Value = data.PositionInTray;
                        worksheet.Cell(currentRow, 12).Value = data.BlockReason;
                        worksheet.Cell(currentRow, 13).Value = data.TestGroup;
                        worksheet.Cell(currentRow, 14).Value = data.TestCode;
                        worksheet.Cell(currentRow, 15).Value = data.Data1;
                        worksheet.Cell(currentRow, 16).Value = data.ReasonCode;
                        worksheet.Cell(currentRow, 17).Value = data.EntryDate;
                        worksheet.Cell(currentRow, 18).Value = data.EntryPerson;
                        worksheet.Cell(currentRow, 19).Value = data.BorrowStatus;
                        worksheet.Cell(currentRow, 20).Value = data.BorrowDate;
                        worksheet.Cell(currentRow, 21).Value = data.BorrowPerson;
                        worksheet.Cell(currentRow, 22).Value = data.Note;
                        worksheet.Cell(currentRow, 23).Value = data.Type;
                    }

                    // Trả file Excel về client
                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "DataScrap.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"System Error: {ex.Message}");
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        [HttpPost("GetLocations")]
        public async Task<IActionResult> GetLocations([FromBody] List<string> serialNumbers)
        {
            if (serialNumbers == null || serialNumbers.Count == 0)
            {
                return BadRequest(new { success = false, message = "Danh sách serialNumbers rỗng." });
            }

            var locationMap = new Dictionary<string, string>();

            var scrapRecords = await _sqlContext.KhoScraps
                .Where(p => serialNumbers.Contains(p.SERIAL_NUMBER))
                .Select(p => new { p.SERIAL_NUMBER, p.ShelfCode, p.ColumnNumber, p.LevelNumber, p.TrayNumber, p.Position })
                .ToListAsync();

            foreach (var r in scrapRecords)
            {
                locationMap[r.SERIAL_NUMBER] = BuildLocation(r.ShelfCode, r.ColumnNumber, r.LevelNumber, r.TrayNumber, r.Position);
            }

            var remaining = serialNumbers.Except(locationMap.Keys).ToList();

            var okRecords = await _sqlContext.KhoOks
                .Where(p => remaining.Contains(p.SERIAL_NUMBER))
                .Select(p => new { p.SERIAL_NUMBER, p.ShelfCode, p.ColumnNumber, p.LevelNumber })
                .ToListAsync();

            foreach (var r in okRecords)
            {
                locationMap[r.SERIAL_NUMBER] = BuildLocation(r.ShelfCode, r.ColumnNumber, r.LevelNumber, null, null);
            }

            remaining = remaining.Except(okRecords.Select(o => o.SERIAL_NUMBER)).ToList();

            var productRecords = await _sqlContext.Products
                .Include(p => p.Shelf)
                .Where(p => remaining.Contains(p.SerialNumber))
                .Select(p => new { SerialNumber = p.SerialNumber, ShelfCode = p.Shelf != null ? p.Shelf.ShelfCode : null, p.ColumnNumber, p.LevelNumber, p.TrayNumber, PositionInTray = p.PositionInTray })
                .ToListAsync();

            foreach (var r in productRecords)
            {
                locationMap[r.SerialNumber] = BuildLocation(r.ShelfCode, r.ColumnNumber, r.LevelNumber, r.TrayNumber, r.PositionInTray);
            }
            //Lay vi tri SN trong RE
            remaining = remaining.Except(productRecords.Select(p => p.SerialNumber)).ToList();
            if (remaining.Any())
            {
                var repairRecords = await _oracleContext.OracleDataRepairTask
                    .Where(r => remaining.Contains(r.SERIAL_NUMBER)).Select(r => new { r.SERIAL_NUMBER, r.DATA18 }).ToListAsync();
                foreach(var r in repairRecords)
                {
                    if (!string.IsNullOrEmpty(r.DATA18))
                    {
                        locationMap[r.SERIAL_NUMBER] = r.DATA18;
                    }
                }
            }
            return Ok(new { success = true, data = locationMap });
        }

        private static string BuildLocation(string shelf, int? column, int? level, int? tray, int? position)
        {
            if (string.IsNullOrEmpty(shelf) || column == null || level == null || tray == null)
            {
                return string.Empty;
            }

            var baseLoc = $"{shelf}{column}-K{level}-{tray}";
            return position.HasValue ? $"{baseLoc}-{position}" : baseLoc;
        }

        private async Task<Dictionary<string, InforProduct>> GetOracleDataAsync(OracleConnection connection, List<string> serialNumbers)
        {
            var productData = new Dictionary<string, InforProduct>();

            if (serialNumbers == null || serialNumbers.Count == 0)
                return productData;

            //Lấy danh sách cặp liên kết từ bảng P_WIP_KEYPARTS_T
            var linkedSerialNumbers = await GetLinkedSerialNumbersAsync(connection, serialNumbers);

            //Xử lý từng cặp SN
            foreach (var originalSerialNumber in serialNumbers)
            {
                //Lấy serial liên kết từ cặp SN nếu tồn tại, nếu không thì giữ SN ban đầu
                var linkedSerialNumber = linkedSerialNumbers.ContainsKey(originalSerialNumber)
                    ? linkedSerialNumbers[originalSerialNumber]
                    : originalSerialNumber;

                //Truy vấn thông tin từ bảng R107
                await FetchR107Data(connection, productData, originalSerialNumber);

                //Truy vấn thông tin từ bảng R109
                await FetchR109Data(connection, productData, originalSerialNumber, linkedSerialNumber);
            }

            return productData;
        }

        private async Task FetchR107Data(OracleConnection connection, Dictionary<string, InforProduct> productData, string originalSerialNumber)
        {
            string queryForR107 = @"
            SELECT
                R107.SERIAL_NUMBER,
                PRODUCT.PRODUCT_LINE,
                R107.MODEL_NAME,
                R107.MO_NUMBER,
                R107.WIP_GROUP,
                R107.WORK_FLAG,
                T_HOLD.HOLD_REASON,
                T_HOLD.HOLD_TIME AS HOLD_TIME_HOLD,
                T_BLOCK.HOLD_REASON AS BLOCK_REASON,
                T_BLOCK.HOLD_TIME AS HOLD_TIME_BLOCK
            FROM SFISM4.R107 R107
            INNER JOIN SFIS1.C_MODEL_DESC_T PRODUCT
            ON R107.MODEL_NAME = PRODUCT.MODEL_NAME
            LEFT JOIN (
                SELECT SERIAL_NUMBER, HOLD_REASON, HOLD_TIME
                FROM SFISM4.R_SYSTEM_HOLD_T
                WHERE (SERIAL_NUMBER, HOLD_TIME) IN (
                    SELECT SERIAL_NUMBER, MAX(HOLD_TIME)
                    FROM SFISM4.R_SYSTEM_HOLD_T
                    WHERE HOLD_REASON IS NOT NULL
                    GROUP BY SERIAL_NUMBER
                )
            ) T_HOLD ON R107.SERIAL_NUMBER = T_HOLD.SERIAL_NUMBER
            LEFT JOIN (
                SELECT SERIAL_NUMBER, HOLD_REASON, HOLD_TIME
                FROM SFISM4.R_SYSTEM_AUTO_BLOCK_T
                WHERE (SERIAL_NUMBER, HOLD_TIME) IN (
                    SELECT SERIAL_NUMBER, MAX(HOLD_TIME)
                    FROM SFISM4.R_SYSTEM_AUTO_BLOCK_T
                    WHERE HOLD_REASON IS NOT NULL
                    GROUP BY SERIAL_NUMBER
                )
            ) T_BLOCK ON R107.SERIAL_NUMBER = T_BLOCK.SERIAL_NUMBER
            WHERE R107.SERIAL_NUMBER = :serialNumber";

            using (var command = new OracleCommand(queryForR107, connection))
            {
                command.Parameters.Add(new OracleParameter("serialNumber", originalSerialNumber));
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        if (!productData.ContainsKey(originalSerialNumber))
                        {
                            productData[originalSerialNumber] = new InforProduct
                            {
                                ModelName = reader["MODEL_NAME"]?.ToString(),
                                ProductLine = reader["PRODUCT_LINE"]?.ToString(),
                                MoNumber = reader["MO_NUMBER"]?.ToString(),
                                WipGroup = reader["WIP_GROUP"]?.ToString(),
                                WorkFlag = reader["WORK_FLAG"]?.ToString(),
                                HoldReason = reader["HOLD_REASON"]?.ToString(),
                                BlockReason = reader["BLOCK_REASON"]?.ToString() // Sửa lỗi: dùng BLOCK_REASON thay vì HOLD_REASON
                            };
                        }
                    }
                }
            }
        }
        private async Task FetchR109Data(OracleConnection connection, Dictionary<string, InforProduct> productData, string originalSerialNumber, string linkedSerialNumber)
        {
            string queryForR109 = $@"
        
            SELECT R109.SERIAL_NUMBER,
                   R109.TEST_GROUP,
                   R109.TEST_CODE,
                   R109.DATA1,
                   R109.REASON_CODE,
                   R109.TEST_TIME
            FROM SFISM4.R109 R109
            WHERE R109.TEST_TIME = (
                SELECT MAX(TEST_TIME)
                FROM SFISM4.R109
                WHERE R109.SERIAL_NUMBER IN ('{linkedSerialNumber}', '{originalSerialNumber}')
            ) AND R109.SERIAL_NUMBER IN ('{linkedSerialNumber}', '{originalSerialNumber}')";

            using (var command = new OracleCommand(queryForR109, connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        // Cập nhật thông tin dựa trên `originalSerialNumber`
                        if (productData.ContainsKey(originalSerialNumber))
                        {
                            productData[originalSerialNumber].TestGroup = reader["TEST_GROUP"]?.ToString();
                            productData[originalSerialNumber].TestCode = reader["TEST_CODE"]?.ToString();
                            productData[originalSerialNumber].Data1 = reader["DATA1"]?.ToString();
                            productData[originalSerialNumber].ReasonCode = reader["REASON_CODE"]?.ToString();
                        }
                    }
                }
            }
        }

        [HttpPost("AdvancedSearch")]
        public async Task<IActionResult> AdvancedSearch([FromBody] AdvancedSearchRequest request, int page = 1, int pageSize = 50)
        {
            try
            {
                var query = from product in _sqlContext.Products
                            join shelf in _sqlContext.Shelves
                            on product.ShelfId equals shelf.ShelfId into shelfGroup
                            from shelf in shelfGroup.DefaultIfEmpty()
                            select new
                            {
                                Product = product,
                                ShelfCode = shelf != null ? shelf.ShelfCode : null
                            };

                if (!string.IsNullOrEmpty(request.BorrowStatus))
                {
                    query = query.Where(productWithShelf =>
                    productWithShelf.Product.BorrowStatus == request.BorrowStatus);
                }
                var sqlSerials = await query.Select(p => p.Product.SerialNumber).ToListAsync();
                if (!sqlSerials.Any())
                {
                    return Ok(new { success = true, totalItems = 0, totalPages = 0, currentPage = page, results = new List<object>() });
                }

                //Kết nối Oracle
                await using var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
                await connection.OpenAsync();

                //Lấy thông tin từ Oracle cho tất cả serialNumbers
                var oracleSerialNumbers = await GetSerialNumbersByData1OrTestCodeAsync(connection, request.WIPGroup, request.TestCode, request.Data1, request.Sfg, sqlSerials);
                //if (!oracleSerialNumbers.Any())
                //{
                //    Console.WriteLine("Không tìm thấy SerialNumbers từ Oracle.");
                //    return Ok(new { success = true, totalItems = 0, totalPages = 0, currentPage = page, results = new List<object>() });
                //}

                var oracleSerialNumbersSet = new HashSet<string>(oracleSerialNumbers);
                query = query.Where(p => oracleSerialNumbersSet.Contains(p.Product.SerialNumber));

                // Phân trang SQL Server
                query = query.OrderBy(p => p.Product.SerialNumber);
                var totalItems = await query.CountAsync();

                //var pagedProducts = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                var pagedProducts = await query
                .OrderBy(productWithShelf => productWithShelf.Product.SerialNumber)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

                var missingSerialNumbers = sqlSerials.Except(oracleSerialNumbers).ToList();

                if (!pagedProducts.Any())
                {
                    return Ok(new { success = true, totalItems, totalPages = 0, currentPage = page, results = new List<object>() });
                }

                //Lấy thông tin chi tiết từ Oracle
                var serialNumbers = pagedProducts.Select(p => p.Product.SerialNumber).ToList();
                var oracleData = await GetOracleDataAsync(connection, serialNumbers);

                //Kết hợp dữ liệu từ Oracle và SQL Server
                var results = pagedProducts.Select(productWithShelf =>
                {
                    var product = productWithShelf.Product;
                    var shelfCode = productWithShelf.ShelfCode;
                    var oracleInfo = oracleData.GetValueOrDefault(product.SerialNumber);
                    return new InforProduct
                    {
                        SerialNumber = product.SerialNumber,
                        ProductLine = product?.ProductLine ?? "",
                        ShelfCode = shelfCode,
                        ColumnNumber = product?.ColumnNumber,
                        LevelNumber = product?.LevelNumber,
                        TrayNumber = product?.TrayNumber,
                        PositionInTray = product?.PositionInTray,
                        EntryDate = product?.EntryDate,
                        BorrowDate = product?.BorrowDate,
                        BorrowPerson = product?.BorrowPerson ?? "",
                        EntryPerson = product?.EntryPerson ?? "",
                        BorrowStatus = product?.BorrowStatus ?? "",
                        Note = product?.Note ?? "",
                        KanBanWIP = product?.KANBAN_WIP ?? "",
                        ActionNe = product?.Action ?? "",
                        Scrap = product?.Scrap ?? "",
                        TestCode = oracleInfo?.TestCode ?? "",
                        Data1 = oracleInfo?.Data1 ?? "",
                        WipGroup = oracleInfo?.WipGroup ?? "",
                        WorkFlag = oracleInfo?.WorkFlag ?? "",
                        TestGroup = oracleInfo?.TestGroup ?? "",
                        MoNumber = oracleInfo?.MoNumber ?? "",
                        ModelName = oracleInfo?.ModelName ?? "",
                        ReasonCode = oracleInfo?.ReasonCode ?? "",
                        HoldReason = oracleInfo?.HoldReason ?? "",
                        BlockReason = oracleInfo?.BlockReason ?? ""
                    };
                }).ToList();

                return Ok(new
                {
                    success = true,
                    totalItems,
                    totalPages = (int)Math.Ceiling((double)totalItems / pageSize),
                    currentPage = page,
                    pageSize,
                    results
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }
        private async Task<List<string>> GetSerialNumbersByData1OrTestCodeAsync(
        OracleConnection connection,
        List<string> wipGroup,
        string testCode,
        string data1,
        List<string> modelName,
        List<string> sqlSerials)
        {
            var serialNumbers = new List<string>();

            if (sqlSerials == null || !sqlSerials.Any())
                return serialNumbers;

            // Chia nhỏ danh sách serials thành các batch
            var batches = sqlSerials
                .Select((value, index) => new { value, index })
                .GroupBy(x => x.index / 1000)
                .Select(group => group.Select(x => x.value).ToList())
                .ToList();

            foreach (var batch in batches)
            {
                var serialNumbersList = string.Join(",", batch.Select(s => $"'{s}'"));

                // Truy vấn để lấy SN liên kết và dữ liệu mới nhất
                string query = $@"
                SELECT
                    COALESCE(LK.OLD_SN, LK.NEW_SN, LT.OLD_SN) AS SERIAL_NUMBER, -- Ưu tiên KEY_PART_SN (OLD_SN)
                    COALESCE(LK.WORK_TIME, LT.TEST_TIME) AS SELECTED_TIME,      -- Ưu tiên WORK_TIME nếu có
                    LT.TEST_CODE,
                    LT.DATA1
                FROM (
                    SELECT 
                        KP.SERIAL_NUMBER AS NEW_SN,       -- Số Serial mới
                        KP.KEY_PART_SN AS OLD_SN,         -- Key Part Serial (Serial cần ưu tiên)
                        KP.WORK_TIME AS WORK_TIME,
                        ROW_NUMBER() OVER (PARTITION BY KP.KEY_PART_SN ORDER BY KP.WORK_TIME DESC) AS RN
                    FROM SFISM4.P_WIP_KEYPARTS_T KP
                    WHERE KP.KEY_PART_SN IN ({serialNumbersList})  -- Lọc theo danh sách SN
                ) LK
                FULL OUTER JOIN (
                    SELECT
                        R109.SERIAL_NUMBER AS OLD_SN,
                        R109.TEST_TIME,
                        R109.TEST_CODE,
                        R109.DATA1,
                        R107.WIP_GROUP,
                        R107.MODEL_NAME,
                        ROW_NUMBER() OVER (PARTITION BY R109.SERIAL_NUMBER ORDER BY R109.TEST_TIME DESC) AS RN
                    FROM SFISM4.R109 R109
                    LEFT JOIN SFISM4.R107 R107 ON R109.SERIAL_NUMBER = R107.SERIAL_NUMBER
                    WHERE R109.SERIAL_NUMBER IN ({serialNumbersList})
                ) LT
                ON LK.OLD_SN = LT.OLD_SN
                WHERE (LK.RN = 1 OR LK.RN IS NULL) AND (LT.RN = 1 OR LT.RN IS NULL)
                ";


                // Thêm các điều kiện tìm kiếm
                if (!string.IsNullOrEmpty(testCode))
                {
                    query += $" AND UPPER(TEST_CODE) LIKE '%{testCode.ToUpper()}%'";
                }

                if (!string.IsNullOrEmpty(data1))
                {
                    query += $" AND UPPER(DATA1) LIKE '%{data1.ToUpper()}%'";
                }

                if (modelName != null && modelName.Any())
                {
                    var modelConditions = string.Join(" OR ", modelName.Select(m => $"UPPER(MODEL_NAME) LIKE '%{m.ToUpper()}%'"));
                    query += $" AND ({modelConditions})";
                }

                if (wipGroup != null && wipGroup.Any())
                {
                    var wipConditions = string.Join(" OR ", wipGroup.Select(w => $"UPPER(WIP_GROUP) LIKE '%{w.ToUpper()}%'"));
                    query += $" AND ({wipConditions})";
                }


                using (var command = new OracleCommand(query, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            serialNumbers.Add(reader["SERIAL_NUMBER"].ToString());
                        }
                    }
                }
            }

            return serialNumbers;
        }

        private async Task<Dictionary<string, string>> GetLinkedSerialNumbersAsync(OracleConnection connection, List<string> serialNumbers)
        {
            var linkedSerialNumbers = new Dictionary<string, string>();

            if (serialNumbers == null || serialNumbers.Count == 0)
                return linkedSerialNumbers;

            //Tạo danh sách tham số SQL
            var serialNumbersList = string.Join(",", serialNumbers.Select(s => $"'{s}'"));

            string queryForSerial2 = $@"
            SELECT SERIAL_NUMBER, KEY_PART_SN
            FROM (
                SELECT
                    SERIAL_NUMBER,
                    KEY_PART_SN,
                    ROW_NUMBER() OVER (PARTITION BY KEY_PART_SN ORDER BY WORK_TIME DESC) AS RN
                FROM SFISM4.P_WIP_KEYPARTS_T
                WHERE KEY_PART_SN IN ({serialNumbersList})
            )
            WHERE RN = 1";

            using (var command = new OracleCommand(queryForSerial2, connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var keyPartSN = reader["KEY_PART_SN"]?.ToString();
                        var serialNumber = reader["SERIAL_NUMBER"]?.ToString();

                        if (!string.IsNullOrEmpty(keyPartSN) && !linkedSerialNumbers.ContainsKey(keyPartSN))
                        {
                            linkedSerialNumbers[keyPartSN] = serialNumber;
                        }
                    }
                }
            }
            return linkedSerialNumbers;
        }

        //===============XUẤT EXCEL=================
        [HttpGet("ExportAllDataToExcel")]
        public async Task<IActionResult> ExportAllDataToExcel()
        {
            try
            {
                // 1. Lấy toàn bộ sản phẩm từ SQL Server
                //var allProducts = await _sqlContext.Products.ToListAsync();
                var allProducts = await _sqlContext.Products
                    .Include(p => p.Shelf) // Thêm thông tin bảng Shelf
                    .ToListAsync();

                if (!allProducts.Any())
                {
                    return BadRequest(new { success = false, message = "Không có dữ liệu để xuất." });
                }

                // 2. Chuẩn bị danh sách serialNumbers
                var serialNumbers = allProducts.Select(p => p.SerialNumber).ToList();

                // 3. Kết nối Oracle và lấy dữ liệu nhóm
                var excelData = new List<InforProduct>();
                await using var connection = new OracleConnection(_oracleContext.Database.GetDbConnection().ConnectionString);
                await connection.OpenAsync();

                // Chia nhỏ serialNumbers thành các batch (tối đa 1000 phần tử mỗi batch)
                var batches = serialNumbers
                    .Select((value, index) => new { value, index })
                    .GroupBy(x => x.index / 1000)
                    .Select(group => group.Select(x => x.value).ToList())
                    .ToList();

                // Dữ liệu từ Oracle cho tất cả serialNumbers
                var oracleData = new Dictionary<string, InforProduct>();

                foreach (var batch in batches)
                {
                    var batchData = await GetOracleDataAsync(connection, batch);
                    foreach (var entry in batchData)
                    {
                        oracleData[entry.Key] = entry.Value;
                    }
                }

                // 4. Kết hợp dữ liệu từ SQL Server và Oracle
                foreach (var product in allProducts)
                {
                    var oracleInfo = oracleData.GetValueOrDefault(product.SerialNumber);

                    excelData.Add(new InforProduct
                    {
                        SerialNumber = product.SerialNumber,
                        ProductLine = product?.ProductLine ?? "",
                        ShelfCode = product?.Shelf?.ShelfCode ?? "",
                        ColumnNumber = product?.ColumnNumber,
                        LevelNumber = product?.LevelNumber,
                        TrayNumber = product?.TrayNumber,
                        PositionInTray = product.PositionInTray,
                        EntryDate = product.EntryDate,
                        BorrowDate = product.BorrowDate,
                        BorrowPerson = product?.BorrowPerson ?? "",
                        EntryPerson = product?.EntryPerson ?? "",
                        BorrowStatus = product?.BorrowStatus ?? "",
                        Note = product?.Note ?? "",
                        KanBanWIP = product?.KANBAN_WIP ?? "",
                        HoldReason = oracleInfo?.HoldReason ?? "",
                        BlockReason = oracleInfo?.BlockReason ?? "",
                        ActionNe = product?.Action ?? "",
                        Scrap = product?.Scrap ?? "",
                        TestCode = oracleInfo?.TestCode ?? "",
                        Data1 = oracleInfo?.Data1 ?? "",
                        WipGroup = oracleInfo?.WipGroup ?? "",
                        TestGroup = oracleInfo?.TestGroup ?? "",
                        MoNumber = oracleInfo?.MoNumber ?? "",
                        ModelName = oracleInfo?.ModelName ?? "",
                        ReasonCode = oracleInfo?.ReasonCode ?? ""
                    });
                }

                // 5. Tạo file Excel
                using (var workbook = new ClosedXML.Excel.XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("All_Data");
                    var currentRow = 1;

                    // Tạo header
                    worksheet.Cell(currentRow, 1).Value = "SERIAL_NUMBER";
                    worksheet.Cell(currentRow, 2).Value = "PRODUCT_LINE";
                    worksheet.Cell(currentRow, 3).Value = "MODEL_NAME";
                    worksheet.Cell(currentRow, 4).Value = "KỆ";
                    worksheet.Cell(currentRow, 5).Value = "CỘT";
                    worksheet.Cell(currentRow, 6).Value = "TẦNG";
                    worksheet.Cell(currentRow, 7).Value = "KHAY";
                    worksheet.Cell(currentRow, 8).Value = "Ô";
                    worksheet.Cell(currentRow, 9).Value = "KANBAN_WIP";
                    worksheet.Cell(currentRow, 10).Value = "HOLD_REASON";
                    worksheet.Cell(currentRow, 11).Value = "BLOCK_REASON";
                    worksheet.Cell(currentRow, 12).Value = "TEST_CODE";
                    worksheet.Cell(currentRow, 13).Value = "ERROR_DESC";
                    worksheet.Cell(currentRow, 14).Value = "WIP_GROUP";
                    worksheet.Cell(currentRow, 15).Value = "WORK_FLAG";
                    worksheet.Cell(currentRow, 16).Value = "TEST_GROUP";
                    worksheet.Cell(currentRow, 17).Value = "MO_NUMBER";
                    worksheet.Cell(currentRow, 18).Value = "REASON_CODE";
                    worksheet.Cell(currentRow, 19).Value = "NGÀY_NHẬP";
                    worksheet.Cell(currentRow, 20).Value = "NGƯỜI_NHẬP";
                    worksheet.Cell(currentRow, 21).Value = "STATUS";
                    worksheet.Cell(currentRow, 22).Value = "NGÀY_MƯỢN";
                    worksheet.Cell(currentRow, 23).Value = "NGƯỜI_MƯỢN";
                    worksheet.Cell(currentRow, 24).Value = "NOTE";
                    worksheet.Cell(currentRow, 25).Value = "ACTION";
                    worksheet.Cell(currentRow, 26).Value = "SCRAP";
                    // Điền dữ liệu
                    foreach (var data in excelData)
                    {
                        currentRow++;
                        worksheet.Cell(currentRow, 1).Value = data.SerialNumber;
                        worksheet.Cell(currentRow, 2).Value = data.ProductLine;
                        worksheet.Cell(currentRow, 3).Value = data.ModelName;
                        worksheet.Cell(currentRow, 4).Value = data.ShelfCode;
                        worksheet.Cell(currentRow, 5).Value = data.ColumnNumber;
                        worksheet.Cell(currentRow, 6).Value = data.LevelNumber;
                        worksheet.Cell(currentRow, 7).Value = data.TrayNumber;
                        worksheet.Cell(currentRow, 8).Value = data.PositionInTray;
                        worksheet.Cell(currentRow, 9).Value = data.KanBanWIP;
                        worksheet.Cell(currentRow, 10).Value = data.HoldReason;
                        worksheet.Cell(currentRow, 11).Value = data.BlockReason;
                        worksheet.Cell(currentRow, 12).Value = data.TestCode;
                        worksheet.Cell(currentRow, 13).Value = data.Data1;
                        worksheet.Cell(currentRow, 14).Value = data.WipGroup;
                        worksheet.Cell(currentRow, 15).Value = data.WorkFlag;
                        worksheet.Cell(currentRow, 16).Value = data.TestGroup;
                        worksheet.Cell(currentRow, 17).Value = data.MoNumber;
                        worksheet.Cell(currentRow, 18).Value = data.ReasonCode;
                        worksheet.Cell(currentRow, 19).Value = data.EntryDate;
                        worksheet.Cell(currentRow, 20).Value = data.EntryPerson;
                        worksheet.Cell(currentRow, 21).Value = data.BorrowStatus;
                        worksheet.Cell(currentRow, 22).Value = data.BorrowDate;
                        worksheet.Cell(currentRow, 23).Value = data.BorrowPerson;
                        worksheet.Cell(currentRow, 24).Value = data.Note;
                        worksheet.Cell(currentRow, 25).Value = data.ActionNe;
                        worksheet.Cell(currentRow, 26).Value = data.Scrap;
                    }

                    // Trả file Excel về client
                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "AllData.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"System Error: {ex.Message}");
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        [HttpPost("ExportSearchResultsToExcel")]
        public async Task<IActionResult> ExportSearchResultsToExcel([FromBody] AdvancedSearchRequest request)
        {
            try
            {
                var allResults = new List<InforProduct>();
                int currentPage = 1;

                while (true)
                {
                    // Gọi API AdvancedSearch từng trang
                    var searchResponse = await AdvancedSearch(request, currentPage, 50); // 50 là kích thước trang
                    if (searchResponse is ObjectResult objectResult && objectResult.Value is { })
                    {
                        dynamic responseData = objectResult.Value;

                        var results = responseData.results as IEnumerable<InforProduct>;
                        if (results == null || !results.Any())
                        {
                            break; // Dừng khi không còn dữ liệu
                        }

                        allResults.AddRange(results);
                        currentPage++;

                        if (currentPage > responseData.totalPages)
                        {
                            break; // Dừng khi hết trang
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                if (!allResults.Any())
                {
                    return StatusCode(500, new { success = false, message = "Không có dữ liệu để xuất ra Excel." });
                }

                // Tiếp tục logic tạo file Excel từ `allResults`
                using (var workbook = new ClosedXML.Excel.XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Search Results");
                    var currentRow = 1;

                    // Tạo header và điền dữ liệu
                    worksheet.Cell(currentRow, 1).Value = "SERIAL_NUMBER";
                    worksheet.Cell(currentRow, 2).Value = "PRODUCT_LINE";
                    worksheet.Cell(currentRow, 3).Value = "MODEL_NAME";
                    worksheet.Cell(currentRow, 4).Value = "KỆ";
                    worksheet.Cell(currentRow, 5).Value = "CỘT";
                    worksheet.Cell(currentRow, 6).Value = "TẦNG";
                    worksheet.Cell(currentRow, 7).Value = "KHAY";
                    worksheet.Cell(currentRow, 8).Value = "Ô";
                    worksheet.Cell(currentRow, 9).Value = "KANBAN_WIP";
                    worksheet.Cell(currentRow, 10).Value = "HOLD_REASON";
                    worksheet.Cell(currentRow, 11).Value = "BLOCK_REASON";
                    worksheet.Cell(currentRow, 12).Value = "TEST_CODE";
                    worksheet.Cell(currentRow, 13).Value = "ERROR_DESC";
                    worksheet.Cell(currentRow, 14).Value = "WIP_GROUP";
                    worksheet.Cell(currentRow, 15).Value = "WORK_FLAG";
                    worksheet.Cell(currentRow, 16).Value = "TEST_GROUP";
                    worksheet.Cell(currentRow, 17).Value = "MO_NUMBER";
                    worksheet.Cell(currentRow, 18).Value = "REASON_CODE";
                    worksheet.Cell(currentRow, 19).Value = "NGÀY_NHẬP";
                    worksheet.Cell(currentRow, 20).Value = "NGƯỜI_NHẬP";
                    worksheet.Cell(currentRow, 21).Value = "STATUS";
                    worksheet.Cell(currentRow, 22).Value = "NGÀY_MƯỢN";
                    worksheet.Cell(currentRow, 23).Value = "NGƯỜI_MƯỢN";
                    worksheet.Cell(currentRow, 24).Value = "NOTE";
                    worksheet.Cell(currentRow, 25).Value = "ACTION";
                    worksheet.Cell(currentRow, 26).Value = "SCRAP";

                    foreach (var result in allResults)
                    {
                        currentRow++;
                        worksheet.Cell(currentRow, 1).Value = result.SerialNumber ?? "N/A";
                        worksheet.Cell(currentRow, 2).Value = result.ProductLine ?? "N/A";
                        // ... (tương tự như trước)
                    }

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "SearchResults.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi xảy ra: {ex.Message}");
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        //GET: /api/modelname
        [HttpGet("ModelName")]
        public async Task<IActionResult> GetModelName()
        {
            try
            {
                var modelName = await _sqlContext.Products.Where(p => p.ModelName != null).Select(p => p.ModelName).Distinct().ToArrayAsync();
                return Ok(modelName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching data", error = ex.Message });
            }
        }

        // GET: api/wipgroup
        [HttpGet("GetWipGroups")]
        public async Task<IActionResult> GetWipGroups()
        {
            try
            {
                // Truy vấn lấy danh sách wip_group khác nhau
                var wipGroups = await _oracleContext.OracleDataR107
                    .Where(r => r.WIP_GROUP != null)
                    .Select(r => r.WIP_GROUP)
                    .Distinct()
                    .OrderBy(w => w)
                    .ToListAsync();

                return Ok(wipGroups); // Trả về danh sách dưới dạng JSON
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching data.", error = ex.Message });
            }
        }

        [HttpPost("SaveSearchList")]
        public async Task<IActionResult> SaveSearchList([FromBody] SaveSearchListRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ListName))
            {
                return BadRequest(new { success = false, message = "Ten danh sach khong duoc de trong!" });
            }
            try
            {
                var searchList = new SearchList
                {
                    ListName = request.ListName,
                    CreatedAt = DateTime.Now,
                    CreatedBy = request.CreatedBy
                };

                _sqlContext.SearchLists.Add(searchList);
                await _sqlContext.SaveChangesAsync();

                var searchListItems = new List<SearchListItem>();
                foreach (var item in request.FoundItems)
                {
                    searchListItems.Add(new SearchListItem
                    {
                        SearchListId = searchList.Id,
                        SerialNumber = item.SerialNumber,
                        ModelName = item.ModelName,
                        ShelfCode = item.ShelfCode,
                        ColumnNumber = item.ColumnNumber,
                        LevelNumber = item.LevelNumber,
                        TrayNumber = item.TrayNumber,
                        PositionInTray = item.PositionInTray,
                        IsFound = true
                    });
                }
                foreach (var item in request.NotFoundItems)
                {
                    string modelName = null;
                    try
                    {
                        string modelNameQuery = @"SELECT SERIAL_NUMBER, MODEL_NAME, MO_NUMBER, WIP_GROUP
                                                FROM SFISM4.R107 
                                                WHERE SERIAL_NUMBER = :serialNumber AND ROWNUM = 1";
                        var modelNameParam = new OracleParameter("serialNumber", OracleDbType.Varchar2)
                        {
                            Value = item.SerialNumber ?? (object)DBNull.Value
                        };
                        var modelNameResult = await _oracleContext.OracleDataR107
                            .FromSqlRaw(modelNameQuery, modelNameParam)
                            .AsNoTracking()
                            .ToListAsync();
                        modelName = modelNameResult.FirstOrDefault()?.MODEL_NAME ?? "N/A";
                    }
                    catch (Exception ex)
                    {
                        modelName = "N/A";
                    }

                    searchListItems.Add(new SearchListItem
                    {
                        SearchListId = searchList.Id,
                        SerialNumber = item.SerialNumber,
                        ModelName = modelName,
                        IsFound = false
                    });
                }
                _sqlContext.SearchListItems.AddRange(searchListItems);
                await _sqlContext.SaveChangesAsync();
                return Ok(new { success = true, message = "Success!!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
        [HttpGet("GetSearchList")]
        public async Task<IActionResult> GetSearchList()
        {
            try
            {
                var searchLists = await _sqlContext.SearchLists.Select(sl => new
                {
                    sl.Id,
                    sl.ListName,
                    sl.CreatedAt,
                    sl.CreatedBy,
                    Items = sl.SearchListItems.Select(i => new
                    {
                        i.SerialNumber,
                        i.ModelName,
                        i.ShelfCode,
                        i.ColumnNumber,
                        i.LevelNumber,
                        i.TrayNumber,
                        i.PositionInTray,
                        i.IsFound
                    }).ToList()
                }).ToListAsync();
                return Ok(new { success = true, data = searchLists });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Xay ra loi!!" });
            }
        }

        public class SaveSearchListRequest
        {
            public string ListName { get; set; }
            public string CreatedBy { get; set; }
            public List<SearchListItem> FoundItems { get; set; }
            public List<SearchListItem> NotFoundItems { get; set; }
        }

        [HttpPost("UpdateScannedStatus")]
        public async Task<IActionResult> UpdateScannedStatus([FromBody] UpdateScannedStatusRequest request)
        {
            try
            {
                var item = await _sqlContext.SearchListItems
                    .FirstOrDefaultAsync(i => i.SearchListId == request.SearchListId && i.SerialNumber == request.SerialNumber);
                if (item == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy mục trong danh sách." });
                }

                item.IsFound = request.isFound;
                await _sqlContext.SaveChangesAsync();
                return Ok(new { success = true, message = "Cập nhật trạng thái quét thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi hệ thống." });
            }
        }

        public class UpdateScannedStatusRequest
        {
            public int SearchListId { get; set; }
            public string SerialNumber { get; set; }
            public bool isFound { get; set; }
        }
    }
}
