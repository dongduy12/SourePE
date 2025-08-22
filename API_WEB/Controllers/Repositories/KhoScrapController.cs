using API_WEB.ModelsDB;
using API_WEB.ModelsOracle;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace API_WEB.Controllers.Repositories
{
    [Route("api/[controller]")]
    [ApiController]
    public class KhoScrapController : ControllerBase
    {
        private readonly CSDL_NE _sqlContext;
        private readonly OracleDbContext _oracleContext;
        private readonly IHttpClientFactory _httpClientFactory;

        public KhoScrapController(CSDL_NE sqlContext, OracleDbContext oracleContext, IHttpClientFactory httpClientFactory)
        {
            _sqlContext = sqlContext;
            _oracleContext = oracleContext;
            _httpClientFactory = httpClientFactory;
        }

        // API 1: Lấy tổng số lượng SN trong bảng Product
        [HttpGet("totalKhoScrap")]
        public async Task<IActionResult> GetTotal()
        {
            try
            {
                var totalCount = await _sqlContext.KhoScraps.CountAsync();
                return Ok(new { success = true, totalCount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("AddScrap")]
        public async Task<IActionResult> AddSNScrap([FromBody] InforSNRequest request)
        {

            if (request == null || request.SerialNumbers == null || !request.SerialNumbers.Any())
            {
                return BadRequest(new { success = false, message = "Du lieu yeu cau khong hop le!!" });
            }
            using var transaction = await _sqlContext.Database.BeginTransactionAsync();

            try
            {
                int maxSlots = 260;
                request.SerialNumbers = request.SerialNumbers.Distinct().ToList();

                ////Kiem tra SerialNumber trong ScrapList
                //var validSerials = await _sqlContext.ScrapLists
                //    .Where(sl => request.SerialNumbers.Contains(sl.SN))
                //    .Select(sl => sl.SN)
                //    .ToListAsync();
                //var invalidSerials = request.SerialNumbers.Except(validSerials).ToList();
                //if (invalidSerials.Any())
                //{
                //    return BadRequest(new
                //    {
                //        success = false,
                //        message = "Một số Serial Number không tồn tại trong ScrapList",
                //        invalidSerials
                //    });
                //}
                var occupiedPositions = await _sqlContext.KhoScraps
                    .Where(p => p.ShelfCode == request.Shelf &&
                    p.ColumnNumber == request.Column &&
                    p.LevelNumber == request.Level &&
                    p.TrayNumber == request.Tray)
                    .Select(p => p.Position.Value).ToListAsync();

                //Kiem tra khay day
                if (occupiedPositions.Count >= maxSlots) 
                { 
                    return BadRequest(new { success = false, message = "Khay đã đầy!" }); 
                }

                var existingProducts = await _sqlContext.KhoScraps
                    .Where(p => request.SerialNumbers.Contains(p.SERIAL_NUMBER))
                    .ToDictionaryAsync(p => p.SERIAL_NUMBER);


                var results = new List<object>();
                var serialsToUpdateOracle = new List<string>();
                foreach (var serialNumber in request.SerialNumbers)
                {
                    var productItem = await _sqlContext.Products.FirstOrDefaultAsync(p => p.SerialNumber == serialNumber);
                    if(productItem != null)
                    {
                        _sqlContext.Products.Remove(productItem);
                    }
                    var okItem = await _sqlContext.KhoOks.FirstOrDefaultAsync(k => k.SERIAL_NUMBER == serialNumber);
                    if(okItem != null)
                    {
                        _sqlContext.KhoOks.Remove(okItem);
                        
                    }
                    if(productItem != null || okItem != null)
                    {
                        await _sqlContext.SaveChangesAsync();
                    }

                    //Kiem tra neu SerialNumber da ton tai
                    var existingProduct = await _sqlContext.KhoScraps.FirstOrDefaultAsync(p => p.SERIAL_NUMBER == serialNumber);
                    if (existingProduct != null)
                    {
                        // Nếu trạng thái BorrowStatus là "Borrowed", cập nhật thông tin kệ
                        if (existingProduct.borrowStatus == "Borrowed")
                        {
                            Console.WriteLine($"Updating product {serialNumber} with new location.");

                            existingProduct.ShelfCode = request.Shelf;
                            existingProduct.ColumnNumber = request.Column;
                            existingProduct.LevelNumber = request.Level;
                            existingProduct.TrayNumber = request.Tray;

                            // Tìm vị trí trống đầu tiên
                            int positionInTray = Enumerable.Range(1, maxSlots).Except(occupiedPositions).FirstOrDefault();
                            if (positionInTray == 0)
                            {
                                results.Add(new { serialNumber, success = false, message = "Không tìm được vị trí trống!" });
                                continue;
                            }

                            existingProduct.Position = positionInTray;
                            existingProduct.borrowStatus = "Available"; // Cập nhật trạng thái
                            existingProduct.entryPerson = request.EntryPerson;
                            existingProduct.entryDate = DateTime.Now;
                            existingProduct.borrowDate = null;
                            existingProduct.borrowPerson = "";
                            occupiedPositions.Add(positionInTray);

                            // Lưu cập nhật vào database
                            _sqlContext.KhoScraps.Update(existingProduct);
                            await _sqlContext.SaveChangesAsync();
                            serialsToUpdateOracle.Add(serialNumber);

                            results.Add(new { serialNumber, success = true, message = "Sản phẩm đã được cập nhật vị trí." });
                        }
                        else { results.Add(new { serialNumber, success = false, message = $"SerialNumber{serialNumber} da ton tai trong he thong" }); }
                        continue;
                    }

                    //Tim vi  tri trong dau tien
                    int positionIntray = Enumerable.Range(1, maxSlots).Except(occupiedPositions).FirstOrDefault();
                    if (positionIntray == 0)
                    {
                        results.Add(new 
                        { 
                            serialNumber, 
                            success = false, 
                            message = "Khong tim duoc vi tri trong!" 
                        });
                        continue;
                    }
                    //Them vi tri vao danh sach da su dung
                    occupiedPositions.Add(positionIntray);

                    //Save san pham vao SQL server
                    var newProduct = new KhoScrap
                    {
                        SERIAL_NUMBER = serialNumber,
                        ShelfCode = request.Shelf,
                        ColumnNumber = request.Column,
                        LevelNumber = request.Level,
                        TrayNumber = request.Tray,
                        Position = positionIntray,
                        entryDate = DateTime.Now,
                        entryPerson = request.EntryPerson
                    };
                    _sqlContext.KhoScraps.Add(newProduct);
                    serialsToUpdateOracle.Add(serialNumber);
                    // Ghi log
                    await LogAction("IMPORT", serialNumber, request.EntryPerson, "");
                    results.Add(new
                    {
                        serialNumber,
                        success = true,
                        message = "Da them san pham thanh cong"
                    });
                }
                await _sqlContext.SaveChangesAsync();

                // Update Oracle location via RepairStatus API
                string location = $"{request.Shelf}{request.Column}-{request.Level}-K{request.Tray}";
                await SendReceivingStatusAsync(serialsToUpdateOracle, request.EntryPerson ?? string.Empty, location, "Nhập(Kho Phế)");

                await transaction.CommitAsync();
                return Ok(new { success = true, results });
            }
            catch (OracleException ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Oracle ERROR: {ex.Message}");
                return StatusCode(500, new { success = false, message = $"Loi Oracle:{ex.Message}" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"System ERROR:{ex.Message}");
                return StatusCode(500, new { sucess = false, message = $"Loi He Thong: {ex.Message}" });
            }

        }

        [HttpGet("TrayInfo")]
        public async Task<IActionResult> GetTrayInfo(string shelf, int column, int level, int tray)
        {
            try
            {
                if (shelf == null)
                {
                    return BadRequest(new { success = false, message = "Ma ke kho nhop le!!" });
                }

                //2. Lay danh sach cac san pham co trong Tray
                var productsInTray = await _sqlContext.KhoScraps.Where(p => p.ShelfCode == shelf &&
                p.ColumnNumber == column &&
                p.LevelNumber == level &&
                p.TrayNumber == tray).OrderBy(p => p.Position).ToListAsync();

                //3. So luong vi tri toi da trong khay
                //int maxSlots = shelf.Contains("XE") ? 20 : 8;
                int maxSlots = 260;
                ////4. Tao danh sach serialnumber da su dung
                //var occupiedSerialNumbers = productsInTray.Select(p => p.SerialNumber).ToList();
                // 4. Tạo danh sách serials với vị trí
                var occupiedPositions = productsInTray.Select(p => new
                {
                    SerialNumber = p.SERIAL_NUMBER,
                    PositionInTray = p.Position
                }).ToList();

                return Ok(new
                {
                    success = true,
                    maxSlots = maxSlots,
                    occupiedCount = occupiedPositions.Count,
                    occupiedPositions = occupiedPositions
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"system ERROR: {ex.Message}");
                return StatusCode(500, new { success = false, message = $"Loi he thong: {ex.Message}" });
            }
        }

        [HttpPost("ExportScrap")]
        public async Task<IActionResult> ExportScrap([FromBody] ExportScrapRequest request)
        {
            if(request == null || request.SerialNumbers == null || !request.SerialNumbers.Any())
            {
                return BadRequest(new ExportScrapResponse
                {
                    Success = false,
                    Message = "Danh sách Serial Number không hợp lệ.",
                    Results = new List<ExportScrapResult>()
                }) ;
            }

            var response = new ExportScrapResponse
            {
                Success = true,
                Results = new List<ExportScrapResult>(),
                Message = ""
            };
            var validSerials = request.SerialNumbers
                .Select(sn => sn?.Trim().ToUpper())
                .Where(sn => !string.IsNullOrEmpty(sn))
                .Distinct()
                .ToList();
            if (!validSerials.Any())
            {
                return BadRequest(new ExportScrapResponse
                {
                    Success = false,
                    Message = "Không có Serial Number hợp lệ để xuất kho.",
                    Results = new List<ExportScrapResult>()
                });
            }

            foreach (var serial in validSerials)
            {
                var result = new ExportScrapResult { SerialNumber = serial };

                try
                {
                    var scrap = await _sqlContext.KhoScraps
                        .FirstOrDefaultAsync(s => s.SERIAL_NUMBER == serial);

                    if (scrap == null)
                    {
                        result.Success = false;
                        result.Message = "Serial Number không tồn tại.";
                        response.Results.Add(result);
                        continue;
                    }

                    // Hard delete
                    _sqlContext.KhoScraps.Remove(scrap);
                    await _sqlContext.SaveChangesAsync();

                    // Ghi log
                    await LogAction("EXPORT_SCRAP", serial, request.ExportPerson, request.Note);

                    result.Success = true;
                    result.Message = "Xuất kho thành công.";
                    response.Results.Add(result);
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"Lỗi khi xuất kho: {ex.Message}";
                    response.Results.Add(result);
                    response.Success = false;
                }
            }

            if (!response.Results.Any(r => r.Success))
            {
                response.Success = false;
                response.Message = "Không xuất kho được Serial Number nào.";
            }
            else if (response.Results.Any(r => !r.Success))
            {
                response.Message = "Một số Serial Number không thể xuất kho.";
            }
            else
            {
                response.Message = "Xuất kho thành công.";
            }

            return Ok(response);
        }

        [HttpGet("ClassifyScrap")]
        public async Task<IActionResult> ClassifyScrap(string category)
        {
            if (string.IsNullOrEmpty(category))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Category không được để trống."
                });
            }

            var validCategories = new[] { "Scrap_done", "Scrap_no_Task", "No_Scrap", "Waiting_Scrap" };
            var categories = category.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                     .Select(c => c.Trim())
                                     .Where(c => validCategories.Contains(c))
                                     .Distinct()
                                     .ToList();

            if (!categories.Any())
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Không có category hợp lệ. Vui lòng sử dụng: 'Scrap_done', 'Scrap_no_Task', 'No_Scrap' hoặc 'Waiting_Scrap'."
                });
            }

            try
            {
                var query = from ks in _sqlContext.KhoScraps.AsNoTracking()
                            join sl in _sqlContext.ScrapLists.AsNoTracking() on ks.SERIAL_NUMBER equals sl.SN into scrapListGroup
                            from sl in scrapListGroup.DefaultIfEmpty()
                            select new ScrapInfo
                            {
                                SerialNumber = ks.SERIAL_NUMBER,
                                ShelfCode = ks.ShelfCode,
                                ColumnNumber = ks.ColumnNumber,
                                LevelNumber = ks.LevelNumber,
                                TrayNumber = ks.TrayNumber,
                                Position = ks.Position,
                                EntryDate = ks.entryDate,
                                EntryPerson = ks.entryPerson,
                                KanBanStatus = sl != null ? sl.KanBanStatus : null,
                                Sloc = sl != null ? sl.Sloc : null,
                                TaskNumber = sl != null ? sl.TaskNumber : null,
                                PO = sl != null ? sl.PO : null,
                                CreatedBy = sl != null ? sl.CreatedBy : null,
                                Cost = sl != null ? sl.Cost : null,
                                InternalTask = sl != null ? sl.InternalTask : null,
                                Description = sl != null ? sl.Desc : null,
                                CreateTime = sl != null ? sl.CreateTime : null,
                                ApproveScrapPerson = sl != null ? sl.ApproveScrapperson : null,
                                ApplyTaskStatus = sl != null ? sl.ApplyTaskStatus : null,
                                FindBoardStatus = sl != null ? sl.FindBoardStatus : null,
                                Remark = sl != null ? sl.Remark : null,
                                Purpose = sl != null ? sl.Purpose : null,
                                Category = sl != null ? sl.Category : null,
                                ApplyTime = sl != null ? sl.ApplyTime : null
                            };

                var scrapList = await query.ToListAsync();

                // Tạo một dictionary để tránh truy vấn lặp lại
                var allScrapList = _sqlContext.ScrapLists.AsNoTracking().ToList();

                var response = new List<object>();

                foreach (var cat in categories)
                {
                    List<ScrapInfo> filteredResults = cat switch
                    {
                        "No_Scrap" => scrapList.Where(r =>
                                        r.TaskNumber == null &&
                                        !allScrapList.Any(sl => sl.SN == r.SerialNumber)).ToList(),

                        "Scrap_done" => scrapList.Where(r => r.TaskNumber != null).ToList(),

                        "Scrap_no_Task" => scrapList.Where(r =>
                                            r.TaskNumber == null &&
                                            allScrapList.Any(sl => sl.SN == r.SerialNumber &&
                                            (sl.ApplyTaskStatus == 1 || sl.ApplyTaskStatus == 0))
                                        ).ToList(),

                        "Waiting_Scrap" => scrapList.Where(r =>
                                                r.TaskNumber == null &&
                                                allScrapList.Any(sl => sl.SN == r.SerialNumber &&
                                                sl.ApplyTaskStatus == 2)
                                            ).ToList(),

                        _ => new List<ScrapInfo>()
                    };

                    response.Add(new
                    {
                        category = cat,
                        count = filteredResults.Count,
                        data = filteredResults
                    });
                }

                return Ok(new
                {
                    success = true,
                    results = response,
                    message = $"Danh sách SerialNumber cho các category: {string.Join(", ", categories)}."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Lỗi hệ thống: {ex.Message}"
                });
            }
        }

        private async Task LogAction(string action, string serialNumber, string user, string note = null)
        {
            _sqlContext.Logs.Add(new LogKhoScrap
            {
                Action = action,
                SerialNumber = serialNumber,
                User = user,
                Note = note,
                Timestamp = DateTime.Now
            });
            await _sqlContext.SaveChangesAsync();
        }

        private async Task UpdateOracleData18Async(IEnumerable<string> serialNumbers, string location)
        {
            if (serialNumbers == null || !serialNumbers.Any()) return;

            var parameters = serialNumbers
                .Select((sn, index) => new OracleParameter($"p{index}", sn))
                .ToList();
            var inClause = string.Join(",", parameters.Select(p => ":" + p.ParameterName));

            var sql = $"UPDATE SFISM4.R_REPAIR_TASK_T SET DATA18 = :location WHERE SERIAL_NUMBER IN ({inClause})";

            parameters.Insert(0, new OracleParameter("location", location));

            await _oracleContext.Database.ExecuteSqlRawAsync(sql, parameters);
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

        [HttpPost("BorrowKhoScrap")]
        public async Task<IActionResult> BorrowKhoScrapSN([FromBody] BorrowSNListRequest request)
        {
            try
            {
                if (request == null || request.SerialNumbers == null || !request.SerialNumbers.Any() || string.IsNullOrEmpty(request.Borrower))
                {
                    return BadRequest(new { success = false, message = "Danh sách Serial Numbers và Borrower là bắt buộc." });
                }

                var borrowedResults = new List<object>();
                var notFoundSerials = new List<string>();
                var failedSerials = new List<string>();
                var borrowHistories = new List<BorrowHistory>();
                foreach (var serialNumber in request.SerialNumbers)
                {
                    try
                    {
                        // Tìm sản phẩm theo SerialNumber
                        var product = await _sqlContext.KhoScraps.FirstOrDefaultAsync(p => p.SERIAL_NUMBER == serialNumber);
                        if (product == null)
                        {
                            notFoundSerials.Add(serialNumber);
                            continue;
                        }
                        // Cập nhật thông tin mượn
                        product.borrowStatus = "Borrowed";
                        product.borrowDate = DateTime.Now;
                        product.borrowPerson = request.Borrower;
                        // Xóa thông tin vị trí
                        product.ShelfCode = null;
                        product.TrayNumber = null;
                        product.LevelNumber = null;
                        product.Position = null;
                        product.ColumnNumber = null;

                        borrowedResults.Add(new
                        {
                            SerialNumber = product.SERIAL_NUMBER,
                            BorrowStatus = product.borrowStatus,
                            BorrowDate = product.borrowDate,
                            BorrowPerson = product.borrowPerson
                        });
                    }
                    catch (Exception ex)
                    {
                        // Ghi nhận lỗi khi xử lý từng serial number
                        failedSerials.Add(serialNumber);
                    }
                }

                // Lưu tất cả thay đổi vào cơ sở dữ liệu
                _sqlContext.BorrowHistories.AddRange(borrowHistories);
                await _sqlContext.SaveChangesAsync();

                // Trả về kết quả
                return Ok(new
                {
                    success = true,
                    totalBorrowed = borrowedResults.Count,
                    totalNotFound = notFoundSerials.Count,
                    totalFailed = failedSerials.Count,
                    borrowedResults,
                    notFoundSerials,
                    failedSerials
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        [HttpGet("totalKhoOk")]
        public async Task<IActionResult> GetTotalKhoOk()
        {
            try
            {
                var totalCount = await _sqlContext.KhoOks.CountAsync();
                return Ok(new { success = true, totalCount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("AddOk")]
        public async Task<IActionResult> AddSNOk([FromBody] InforSNRequest request)
        {

            if (request == null || request.SerialNumbers == null || !request.SerialNumbers.Any())
            {
                return BadRequest(new { success = false, message = "Du lieu yeu cau khong hop le!!" });
            }
            using var transaction = await _sqlContext.Database.BeginTransactionAsync();

            try
            {
                int maxSlots = 160;
                request.SerialNumbers = request.SerialNumbers.Distinct().ToList();

                //Kiem tra SerialNumber trong ScrapList
                var validSerials = await _sqlContext.ScrapLists
                    .Where(sl => request.SerialNumbers.Contains(sl.SN))
                    .Select(sl => sl.SN)
                    .ToListAsync();
                var invalidSerials = request.SerialNumbers.Except(validSerials).ToList();
                if (!invalidSerials.Any())
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Serial Number tồn tại trong ScrapList",
                        invalidSerials
                    });
                }

                var existingProducts = await _sqlContext.KhoOks
                    .Where(p => request.SerialNumbers.Contains(p.SERIAL_NUMBER))
                    .ToDictionaryAsync(p => p.SERIAL_NUMBER);

                var results = new List<object>();
                var serialsToUpdateOracle = new List<string>();

                foreach (var serialNumber in request.SerialNumbers)
                {
                    var productItem = await _sqlContext.Products.FirstOrDefaultAsync(p => p.SerialNumber == serialNumber);
                    if(productItem != null)
                    {
                        _sqlContext.Products.Remove(productItem);
                    }
                    var scrapItem = await _sqlContext.KhoScraps.FirstOrDefaultAsync(k => k.SERIAL_NUMBER == serialNumber);
                    if(scrapItem != null)
                    {
                        _sqlContext.KhoScraps.Remove(scrapItem);
                    }
                    if(productItem != null || scrapItem != null)
                    {
                        await _sqlContext.SaveChangesAsync();
                    }
                    //Kiem tra neu SerialNumber da ton tai
                    var existingProduct = await _sqlContext.KhoOks.FirstOrDefaultAsync(p => p.SERIAL_NUMBER == serialNumber);
                    if (existingProduct != null)
                    {
                        // Nếu trạng thái BorrowStatus là "Borrowed", cập nhật thông tin kệ
                        if (existingProduct.borrowStatus == "Borrowed")
                        {
                            Console.WriteLine($"Updating product {serialNumber} with new location.");

                            existingProduct.ShelfCode = request.Shelf;
                            existingProduct.ColumnNumber = request.Column;
                            existingProduct.LevelNumber = request.Level;
                            existingProduct.borrowStatus = "Available"; // Cập nhật trạng thái
                            existingProduct.entryPerson = request.EntryPerson;
                            existingProduct.entryDate = DateTime.Now;
                            existingProduct.borrowDate = null;
                            existingProduct.borrowPerson = "";
                            // Lưu cập nhật vào database
                            _sqlContext.KhoOks.Update(existingProduct);
                            await _sqlContext.SaveChangesAsync();

                            results.Add(new { serialNumber, success = true, message = "Sản phẩm đã được cập nhật vị trí." });
                        }
                        else { results.Add(new { serialNumber, success = false, message = $"SerialNumber{serialNumber} da ton tai trong he thong" }); }
                        continue;
                    }
                    //Save san pham vao SQL server
                    var newProduct = new KhoOk
                    {
                        SERIAL_NUMBER = serialNumber,
                        ShelfCode = request.Shelf,
                        ColumnNumber = request.Column,
                        LevelNumber = request.Level,
                        entryDate = DateTime.Now,
                        entryPerson = request.EntryPerson
                    };
                    _sqlContext.KhoOks.Add(newProduct);
                    serialsToUpdateOracle.Add(serialNumber);
                    // Ghi log
                    await LogAction("IMPORT_KHO_OK", serialNumber, request.EntryPerson, "");
                    results.Add(new
                    {
                        serialNumber,
                        success = true,
                        message = "Da them san pham thanh cong"
                    });
                }
                await _sqlContext.SaveChangesAsync();
                // Update Oracle location via RepairStatus API
                string location = $"{request.Shelf}{request.Column}-{request.Level}";
                await SendReceivingStatusAsync(serialsToUpdateOracle, request.EntryPerson ?? string.Empty, location, "Nhập(Kho Ok)");

                await transaction.CommitAsync();
                return Ok(new { success = true, results });
            }
            catch (OracleException ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Oracle ERROR: {ex.Message}");
                return StatusCode(500, new { success = false, message = $"Loi Oracle:{ex.Message}" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"System ERROR:{ex.Message}");
                return StatusCode(500, new { sucess = false, message = $"Loi He Thong: {ex.Message}" });
            }

        }

        [HttpPost("ExportKhoOk")]
        public async Task<IActionResult> ExportKhoOk([FromBody] ExportScrapRequest request)
        {
            if (request == null || request.SerialNumbers == null || !request.SerialNumbers.Any())
            {
                return BadRequest(new ExportScrapResponse
                {
                    Success = false,
                    Message = "Danh sách Serial Number không hợp lệ.",
                    Results = new List<ExportScrapResult>()
                });
            }

            var response = new ExportScrapResponse
            {
                Success = true,
                Results = new List<ExportScrapResult>(),
                Message = ""
            };
            var validSerials = request.SerialNumbers
                .Select(sn => sn?.Trim().ToUpper())
                .Where(sn => !string.IsNullOrEmpty(sn))
                .Distinct()
                .ToList();
            if (!validSerials.Any())
            {
                return BadRequest(new ExportScrapResponse
                {
                    Success = false,
                    Message = "Không có Serial Number hợp lệ để xuất kho.",
                    Results = new List<ExportScrapResult>()
                });
            }

            foreach (var serial in validSerials)
            {
                var result = new ExportScrapResult { SerialNumber = serial };

                try
                {
                    var snOk = await _sqlContext.KhoOks
                        .FirstOrDefaultAsync(s => s.SERIAL_NUMBER == serial);

                    if (snOk == null)
                    {
                        result.Success = false;
                        result.Message = "Serial Number không tồn tại.";
                        response.Results.Add(result);
                        continue;
                    }

                    // Hard delete
                    _sqlContext.KhoOks.Remove(snOk);
                    await _sqlContext.SaveChangesAsync();

                    // Ghi log
                    await LogAction("EXPORT_KHO_OK", serial, request.ExportPerson, request.Note);

                    result.Success = true;
                    result.Message = "Xuất kho thành công.";
                    response.Results.Add(result);
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"Lỗi khi xuất kho: {ex.Message}";
                    response.Results.Add(result);
                    response.Success = false;
                }
            }

            if (!response.Results.Any(r => r.Success))
            {
                response.Success = false;
                response.Message = "Không xuất kho được Serial Number nào.";
            }
            else if (response.Results.Any(r => !r.Success))
            {
                response.Message = "Một số Serial Number không thể xuất kho.";
            }
            else
            {
                response.Message = "Xuất kho thành công.";
            }

            return Ok(response);
        }

        [HttpPost("BorrowKhoOk")]
        public async Task<IActionResult> BorrowKhoOk([FromBody] BorrowSNListRequest request)
        {
            try
            {
                if (request == null || request.SerialNumbers == null || !request.SerialNumbers.Any() || string.IsNullOrEmpty(request.Borrower))
                {
                    return BadRequest(new { success = false, message = "Danh sách Serial Numbers và Borrower là bắt buộc." });
                }

                var borrowedResults = new List<object>();
                var notFoundSerials = new List<string>();
                var failedSerials = new List<string>();
                var borrowHistories = new List<BorrowHistory>();
                foreach (var serialNumber in request.SerialNumbers)
                {
                    try
                    {
                        // Tìm sản phẩm theo SerialNumber
                        var product = await _sqlContext.KhoOks.FirstOrDefaultAsync(p => p.SERIAL_NUMBER == serialNumber);
                        if (product == null)
                        {
                            notFoundSerials.Add(serialNumber);
                            continue;
                        }
                        // Cập nhật thông tin mượn
                        product.borrowStatus = "Borrowed";
                        product.borrowDate = DateTime.Now;
                        product.borrowPerson = request.Borrower;
                        // Xóa thông tin vị trí
                        product.ShelfCode = null;
                        product.LevelNumber = null;
                        product.ColumnNumber = null;
                        borrowedResults.Add(new
                        {
                            SerialNumber = product.SERIAL_NUMBER,
                            BorrowStatus = product.borrowStatus,
                            BorrowDate = product.borrowDate,
                            BorrowPerson = product.borrowPerson
                        });
                    }
                    catch (Exception ex)
                    {
                        // Ghi nhận lỗi khi xử lý từng serial number
                        failedSerials.Add(serialNumber);
                    }
                }
                await _sqlContext.SaveChangesAsync();

                // Trả về kết quả
                return Ok(new
                {
                    success = true,
                    totalBorrowed = borrowedResults.Count,
                    totalNotFound = notFoundSerials.Count,
                    totalFailed = failedSerials.Count,
                    borrowedResults,
                    notFoundSerials,
                    failedSerials
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        public class InforSNRequest
        {
            public string? Shelf { get; set; }
            public int Column { get; set; }
            public int Level { get; set; }
            public int Tray { get; set; }
            public string? EntryPerson { get; set; }
            public List<string>? SerialNumbers { get; set; }
        }
        public class ScrapInfo
        {
            public string SerialNumber { get; set; } = null!;
            public string? ShelfCode { get; set; }
            public int? ColumnNumber { get; set; }
            public int? LevelNumber { get; set; }
            public int? TrayNumber { get; set; }
            public int? Position { get; set; }
            public DateTime? EntryDate { get; set; }
            public string? EntryPerson { get; set; }
            public string? KanBanStatus { get; set; }
            public string? Sloc { get; set; }
            public string? TaskNumber { get; set; }
            public string? PO { get; set; }
            public string? CreatedBy { get; set; }
            public string? Cost { get; set; }
            public string? InternalTask { get; set; }
            public string? Description { get; set; }
            public DateTime? CreateTime { get; set; }
            public string? ApproveScrapPerson { get; set; }
            public int? ApplyTaskStatus { get; set; }
            public string? FindBoardStatus { get; set; }
            public string? Remark { get; set; }
            public string? Purpose { get; set; }
            public string? Category { get; set; }
            public DateTime? ApplyTime { get; set; }
        }
        public class ExportScrapRequest
        {
            public List<string> SerialNumbers { get; set; } = null!;
            public string ExportPerson { get; set; } = null!;
            public string? Note { get; set; }
        }
        public class ExportScrapResult
        {
            public string SerialNumber { get; set; } = null!;
            public bool Success { get; set; }
            public string Message { get; set; } = null!;
        }
        public class ExportScrapResponse
        {
            public bool Success { get; set; }
            public List<ExportScrapResult> Results { get; set; } = null!;
            public string Message { get; set; } = null!;
        }

    }
}
