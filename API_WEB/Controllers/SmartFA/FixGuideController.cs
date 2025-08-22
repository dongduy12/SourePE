using API_WEB.ModelsDB;
using API_WEB.ModelsOracle;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using Oracle.ManagedDataAccess.Client;
using System.Text.RegularExpressions;

namespace API_WEB.Controllers.SmartFA
{
    [Route("api/[controller]")]
    [ApiController]
    public class FixGuideController : ControllerBase
    {
        private readonly CSDL_NE _sqlContext;
        private readonly OracleDbContext _oracleContext;

        public FixGuideController(CSDL_NE sqlContext, OracleDbContext oracleContext)
        {
            _sqlContext = sqlContext;
            _oracleContext = oracleContext;
        }
        [HttpGet("GetProductLines")]
        public async Task<IActionResult> GetProductLines()
        {
            try
            {
                var productLines = await _sqlContext.Products
                    .Select(p => p.ProductLine)
                    .Distinct()
                    .ToListAsync();

                return Ok(new { success = true, productLines });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        [HttpPost("Upload")]
        public async Task<IActionResult> UploadGuide(IFormFile file, string productLine)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "File không hợp lệ." });

            if (string.IsNullOrEmpty(productLine))
                return BadRequest(new { success = false, message = "ProductLine không được để trống." });

            try
            {
                // Chuẩn hóa ProductLine: Bỏ khoảng trắng, dấu '-' và viết hoa
                string standardizedProductLine = productLine.Replace(" ", "").Replace("-", "").ToUpper();

                // Thiết lập LicenseContext
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                // Đọc nội dung file Excel
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;
                using var package = new ExcelPackage(stream);
                var worksheet = package.Workbook.Worksheets.FirstOrDefault();

                if (worksheet == null)
                    return BadRequest(new { success = false, message = "Không tìm thấy sheet trong file Excel." });

                // Xóa dữ liệu cũ theo ProductLine (nếu cần)
                var existingGuides = _sqlContext.GuideRecords
                    .Where(g => g.ProductLine.Replace(" ", "").Replace("-", "").ToUpper() == standardizedProductLine);
                _sqlContext.GuideRecords.RemoveRange(existingGuides);
                await _sqlContext.SaveChangesAsync();

                // Lấy số hàng trong sheet
                int rowCount = worksheet.Dimension.Rows;

                // Thêm dữ liệu từ file Excel vào database
                for (int row = 2; row <= rowCount; row++) // Bỏ qua tiêu đề (hàng đầu tiên)
                {
                    string testCode = worksheet.Cells[row, 3].Text.Trim();
                    string checkPoint = worksheet.Cells[row, 6].Text.Trim();

                    if (!string.IsNullOrEmpty(testCode) && !string.IsNullOrEmpty(checkPoint))
                    {
                        _sqlContext.GuideRecords.Add(new GuideRecords
                        {
                            ProductLine = standardizedProductLine, // Gán ProductLine đã chuẩn hóa
                            Check_Point = checkPoint,
                            TestCode = testCode
                        });
                    }
                }

                await _sqlContext.SaveChangesAsync();
                return Ok(new { success = true, message = "File đã được upload và lưu thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Đã xảy ra lỗi: {ex.Message}" });
            }
        }

        [HttpPost("UploadUser")]
        public async Task<IActionResult> UploadUser(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "File không hợp lệ." });

            try
            {
                // Thiết lập LicenseContext
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                // Đọc nội dung file Excel
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                using var package = new ExcelPackage(stream);
                var worksheet = package.Workbook.Worksheets.FirstOrDefault();

                if (worksheet == null)
                    return BadRequest(new { success = false, message = "Không tìm thấy sheet trong file Excel." });

                // Nếu muốn xóa hết dữ liệu trước khi nhập mới
                _sqlContext.Users.RemoveRange(_sqlContext.Users);
                await _sqlContext.SaveChangesAsync();

                // Lấy số hàng trong sheet
                int rowCount = worksheet.Dimension.Rows;

                // Danh sách lưu dữ liệu mới
                var newUsers = new List<User>();

                // Duyệt qua từng dòng, bỏ qua dòng đầu tiên (tiêu đề)
                for (int row = 2; row <= rowCount; row++)
                {
                    string userCode = worksheet.Cells[row, 2].Text.Trim();
                    string passwordHash = worksheet.Cells[row, 3].Text.Trim();
                    string role = worksheet.Cells[row, 4].Text.Trim();
                    string permissions = worksheet.Cells[row, 5].Text.Trim();
                    string fullName = worksheet.Cells[row, 6].Text.Trim();
                    string email = worksheet.Cells[row, 7].Text.Trim();
                    string status = worksheet.Cells[row, 8].Text.Trim();

                    // Chỉ thêm nếu có userCode (bắt buộc)
                    if (!string.IsNullOrEmpty(userCode))
                    {
                        newUsers.Add(new User
                        {
                            Username = userCode,
                            Password = passwordHash,
                            Role = role,
                            AllowedAreas = permissions,
                            FullName = fullName,
                            Email = email,
                            Department = status
                        });
                    }
                }

                // Thêm vào database
                if (newUsers.Count > 0)
                {
                    await _sqlContext.Users.AddRangeAsync(newUsers);
                    await _sqlContext.SaveChangesAsync();
                }

                return Ok(new { success = true, message = "Dữ liệu đã được upload thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Đã xảy ra lỗi: {ex.Message}",
                    details = ex.InnerException?.Message
                });
            }
        }


        [HttpPost("import-excel-ne")]
        public async Task<IActionResult> addSNExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "File không hợp lệ." });

            try
            {
                // Thiết lập LicenseContext
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                // Đọc nội dung file Excel
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                using var package = new ExcelPackage(stream);
                var worksheet = package.Workbook.Worksheets.FirstOrDefault();

                if (worksheet == null)
                    return BadRequest(new { success = false, message = "Không tìm thấy sheet trong file Excel." });

                // Xóa hết dữ liệu cũ trước khi nhập mới
                _sqlContext.ProductOlds.RemoveRange(_sqlContext.ProductOlds);
                await _sqlContext.SaveChangesAsync();

                // Lấy số hàng trong sheet
                int rowCount = worksheet.Dimension.Rows;

                // Danh sách lưu dữ liệu mới
                var productTests = new List<ProductOld>();

                // Duyệt qua từng dòng, bỏ qua dòng đầu tiên (tiêu đề)
                for (int row = 2; row <= rowCount; row++)
                {
                    string serialNumber = worksheet.Cells[row, 1].Text.Trim();
                    string productLine = worksheet.Cells[row, 2].Text.Trim();
                    string modelName = worksheet.Cells[row, 3].Text.Trim();
                    string moNumber = worksheet.Cells[row, 4].Text.Trim();
                    string wipGroup = worksheet.Cells[row, 5].Text.Trim();
                    string workFlag = worksheet.Cells[row, 6].Text.Trim();
                    string testGroup = worksheet.Cells[row, 7].Text.Trim();
                    string reasonCode = worksheet.Cells[row, 8].Text.Trim();
                    string testCode = worksheet.Cells[row, 9].Text.Trim();
                    string errorDesc = worksheet.Cells[row, 10].Text.Trim();
                    string kanbanWIP = worksheet.Cells[row, 11].Text.Trim();

                    // Chỉ thêm nếu có SerialNumber (bắt buộc)
                    if (!string.IsNullOrEmpty(serialNumber))
                    {
                        productTests.Add(new ProductOld
                        {
                            SERIAL_NUMBER = serialNumber,
                            PRODUCT_LINE = productLine,
                            MODEL_NAME = modelName,
                            MO_NUMBER = moNumber,
                            WIP_GROUP = wipGroup,
                            WORK_FLAG = workFlag,
                            TEST_GROUP = testGroup,
                            REASON_CODE = reasonCode,
                            TEST_CODE = testCode,
                            ERROR_DESC = errorDesc,
                            KANBAN_WIP = kanbanWIP
                        });
                    }
                }

                // Thêm vào database
                if (productTests.Count > 0)
                {
                    await _sqlContext.ProductOlds.AddRangeAsync(productTests);
                    await _sqlContext.SaveChangesAsync();
                }

                return Ok(new { success = true, message = "Dữ liệu đã được upload thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Đã xảy ra lỗi: {ex.Message}",
                    details = ex.InnerException?.Message
                });
            }
        }

        /// <summary>
        /// Edit checkpoint
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPut("update-checkpoint")]
        public async Task<IActionResult> UpdateCheckPoint([FromBody] UpdateCheckPointDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.TestCode) || string.IsNullOrWhiteSpace(dto.ProductLine))
            {
                return BadRequest("Invalid input data.");
            }

            // Chuẩn hóa ProductLine
            dto.ProductLine = NormalizeProductLine(dto.ProductLine);

            var record = await _sqlContext.Set<GuideRecords>()
                .FirstOrDefaultAsync(r => r.TestCode == dto.TestCode && r.ProductLine == dto.ProductLine);

            if (record == null)
            {
                // Tạo mới bản ghi nếu không tìm thấy
                var newRecord = new GuideRecords
                {
                    TestCode = dto.TestCode,
                    ProductLine = dto.ProductLine,
                    Check_Point = dto.Check_Point
                };

                await _sqlContext.Set<GuideRecords>().AddAsync(newRecord);
                await _sqlContext.SaveChangesAsync();

                return Ok(new { success = true, message = "Đã tạo mới bản ghi và thêm Check_Point thành công." });
            }

            // Cập nhật Check_Point nếu bản ghi đã tồn tại
            record.Check_Point = dto.Check_Point;
            await _sqlContext.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã cập nhật Check_Point thành công." });
        }
        private string NormalizeProductLine(string productLine)
        {
            return Regex.Replace(productLine.ToUpper(), @"[\s-]", "");
        }

        [HttpPost("UploadGuideCheckIn")]
        public async Task<IActionResult> UploadGuideCheckIn(IFormFile file, [FromForm] string productLine)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "File không hợp lệ." });

            if (string.IsNullOrEmpty(productLine))
                return BadRequest(new { success = false, message = "ProductLine không được để trống." });

            try
            {
                // Thiết lập LicenseContext
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                // Đọc nội dung file Excel
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                using var package = new ExcelPackage(stream);
                var worksheet = package.Workbook.Worksheets.FirstOrDefault();

                if (worksheet == null)
                    return BadRequest(new { success = false, message = "Không tìm thấy sheet trong file Excel." });

                // Xóa dữ liệu cũ theo ProductLine (nếu cần)
                var existingGuides = _sqlContext.CheckInGuides.Where(g => g.ProductLine == productLine);
                _sqlContext.CheckInGuides.RemoveRange(existingGuides);
                await _sqlContext.SaveChangesAsync();

                // Lấy số hàng trong sheet
                int rowCount = worksheet.Dimension.Rows;

                // Thêm dữ liệu từ file Excel vào database
                for (int row = 2; row <= rowCount; row++) // Bỏ qua tiêu đề (hàng đầu tiên)
                {
                    string errorCode = worksheet.Cells[row, 2].Text.Trim();
                    string guide = worksheet.Cells[row, 4].Text.Trim();

                    if (!string.IsNullOrEmpty(errorCode) && !string.IsNullOrEmpty(guide))
                    {
                        _sqlContext.CheckInGuides.Add(new CheckInGuides
                        {
                            ProductLine = productLine, // Gán ProductLine
                            Guide = guide,
                            ErrorCode = errorCode
                        });
                    }
                }
                await _sqlContext.SaveChangesAsync();
                return Ok(new { success = true, message = "File đã được upload và lưu thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Đã xảy ra lỗi: {ex.Message}" });
            }
        }

        [HttpPost("GetCheckPoints")]
        public async Task<IActionResult> GetCheckPoints([FromBody] CheckPointRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ProductLine) || string.IsNullOrEmpty(request.TestCode))
                {
                    return BadRequest(new { success = false, message = "ProductLine hoặc Data1 rỗng." });
                }

                // Chuẩn hóa ProductLine: viết hoa, bỏ khoảng trắng và dấu '-'
                string normalizedProductLine = NormalizeProductLine(request.ProductLine);


                // Truy vấn cơ sở dữ liệu để lấy CheckPoints
                var checkPoints = await _sqlContext.GuideRecords
                    .Where(g => g.ProductLine == normalizedProductLine && g.TestCode == request.TestCode)
                    .Select(g => g.Check_Point)
                    .ToListAsync();

                if (!checkPoints.Any())
                {
                    return NotFound(new { success = false, message = "Không tìm thấy CheckPoint phù hợp." });
                }

                return Ok(new { success = true, checkPoints });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }


        /// <summary>
        /// Lấy guide checkIn
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("GetCheckIn")]
        public async Task<IActionResult> GetCheckIn([FromBody] CheckInRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ProductLine) || string.IsNullOrEmpty(request.ErrorCode))
                {
                    return BadRequest(new { success = false, message = "ProductLine hoặc Data1 rỗng." });
                }

                // Loại bỏ dấu ';' ở cuối data1 nếu có
                //string normalizedData1 = request.TestCode.TrimEnd(';');

                // Truy vấn cơ sở dữ liệu để lấy guide
                var Guides = await _sqlContext.CheckInGuides
                    .Where(g => g.ProductLine == request.ProductLine && g.ErrorCode == request.ErrorCode)
                    .Select(g => g.Guide)
                    .ToListAsync();

                if (!Guides.Any())
                {
                    return NotFound(new { success = false, message = "Không tìm thấy CheckPoint phù hợp." });
                }

                return Ok(new { success = true, Guides});
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }


        public class CheckPointRequest
        {
            public string ProductLine { get; set; }
            public string TestCode { get; set; }
        }
        public class CheckInRequest
        {
            public string ProductLine { get; set; }
            public string ErrorCode { get; set; }
        }
        public class UpdateCheckPointDto
        {
            public string TestCode { get; set; }
            public string ProductLine { get; set; }
            public string Check_Point { get; set; }
        }

        /// <summary>
        /// LƯU LỊCH SỬ SỬA CHỮA
        /// </summary>
        /// <param name="repairHistory"></param>
        /// <returns></returns>
        [HttpPost("SaveRepairHistoryWithActions")]
        public async Task<IActionResult> SaveRepairHistoryWithActions([FromBody] RepairHistory repairHistory)
        {
            if (repairHistory == null || repairHistory.RepairActions == null || !repairHistory.RepairActions.Any())
            {
                return BadRequest("Thông tin không hợp lệ.");
            }

            try
            {
                // Kiểm tra xem Serial Number đã tồn tại chưa
                var existingRepairHistory = await _sqlContext.RepairHistory
                    .Include(rh => rh.RepairActions)
                    .FirstOrDefaultAsync(rh => rh.SerialNumber == repairHistory.SerialNumber);

                if (existingRepairHistory != null)
                {
                    // Cập nhật các hành động sửa chữa vào bản ghi hiện có
                    foreach (var action in repairHistory.RepairActions)
                    {
                        existingRepairHistory.RepairActions.Add(action);
                    }

                    // Cập nhật lại thời gian sửa chữa (nếu cần)
                    existingRepairHistory.RepairTime = DateTime.UtcNow;
                }
                else
                {
                    // Thêm bản ghi mới nếu chưa tồn tại
                    _sqlContext.RepairHistory.Add(repairHistory);
                }

                // Lưu thay đổi
                await _sqlContext.SaveChangesAsync();

                return Ok("Lịch sử sửa chữa và các hành động đã được lưu thành công.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi khi lưu dữ liệu: {ex.Message}");
            }
        }

        [HttpGet("GetRepairHistoryBySerialNumber/{serialNumber}")]
        public async Task<IActionResult> GetRepairHistoryBySerialNumber(string serialNumber)
        {
            if (string.IsNullOrEmpty(serialNumber))
            {
                return BadRequest("Serial Number không hợp lệ.");
            }

            try
            {
                // Truy vấn lịch sử sửa chữa theo Serial Number
                var repairHistories = await _sqlContext.RepairHistory
                    .Include(rh => rh.RepairActions) // Bao gồm các hành động sửa chữa
                    .Where(rh => rh.SerialNumber == serialNumber)
                    .OrderByDescending(rh => rh.RepairTime) // Sắp xếp theo thời gian sửa chữa mới nhất
                    .ToListAsync();

                if (repairHistories == null || !repairHistories.Any())
                {
                    return NotFound($"Không tìm thấy lịch sử sửa chữa cho Serial Number: {serialNumber}");
                }

                // Chuẩn bị dữ liệu trả về
                var result = repairHistories.Select(rh => new
                {
                    rh.Id,
                    rh.SerialNumber,
                    rh.ModelName,
                    rh.ProductLine,
                    rh.RepairTime,
                    rh.CreatedAt,
                    RepairActions = rh.RepairActions.Select(ra => new
                    {
                        ra.Id,
                        ra.ActionDescription,
                        ra.ActionTime,
                        ra.Data1,
                        ra.ResponsiblePerson,
                        ra.Note
                    })
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi khi lấy dữ liệu: {ex.Message}");
            }
        }
    }
}
