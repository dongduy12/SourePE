#nullable disable
using API_WEB.ModelsDB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API_WEB.Controllers.Repositories
{
    [Route("api/[controller]")]
    [ApiController]
    public class BorrowController : ControllerBase
    {
        private readonly CSDL_NE _sqlContext;

        public BorrowController(CSDL_NE sqlContext)
        {
            _sqlContext = sqlContext;
        }

        /// <summary>
        /// API cho mượn danh sách serial numbers
        /// </summary>
        /// <param name="request">Request chứa thông tin danh sách SNs và Borrower</param>
        /// <returns>Kết quả cho mượn</returns>
        [HttpPost("Borrow")]
        public async Task<IActionResult> BorrowSNs([FromBody] BorrowSNListRequest request)
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
                        var product = await _sqlContext.Products.FirstOrDefaultAsync(p => p.SerialNumber == serialNumber);
                        if (product == null)
                        {
                            notFoundSerials.Add(serialNumber);
                            continue;
                        }
                        // Cập nhật thông tin mượn
                        product.BorrowStatus = "Borrowed";
                        product.BorrowDate = DateTime.Now;
                        product.BorrowPerson = request.Borrower;
                        // Xóa thông tin vị trí
                        product.ShelfId = null;
                        product.TrayNumber = null;
                        product.LevelNumber = null;
                        product.PositionInTray = null;
                        product.ColumnNumber = null;
                        borrowHistories.Add(new BorrowHistory
                        {
                            SerialNumber = product.SerialNumber,
                            BorrowPerson = request.Borrower,
                            BorrowDate = DateTime.Now,
                            ProductLine = product.ProductLine,
                            ModelName = product.ModelName
                        });
                        borrowedResults.Add(new
                        {
                            SerialNumber = product.SerialNumber,
                            BorrowStatus = product.BorrowStatus,
                            BorrowDate = product.BorrowDate,
                            BorrowPerson = product.BorrowPerson
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

        /// <summary>
        /// API tìm kiếm Serial Number từ bảng BorrowHistory
        /// </summary>
        /// <param name="serialNumber">Serial Number cần tìm</param>
        /// <returns>Thông tin lịch sử mượn</returns>
        [HttpPost("Search")]
        public async Task<IActionResult> SearchSN([FromBody] List<string> serialNumbers)
        {
            try
            {
                if (serialNumbers == null || !serialNumbers.Any())
                {
                    return BadRequest(new { success = false, message = "Serial Number không được để trống." });
                }

                var borrowHistory = await _sqlContext.BorrowHistories
                    .Where(bh => serialNumbers.Contains(bh.SerialNumber)) //Sắp xếp theo nhóm SN
                    .OrderByDescending(bh => bh.SerialNumber).ThenByDescending(bh => bh.BorrowDate) // Lấy lịch sử mới nhất trước
                    .ToListAsync();

                if (borrowHistory == null || !borrowHistory.Any())
                {
                    return NotFound(new { success = false, message = "Không tìm thấy lịch sử mượn cho Serial Number này." });
                }

                return Ok(new
                {
                    success = true,
                    totalRecords = borrowHistory.Count,
                    data = borrowHistory
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }


    }

    /// <summary>
    /// Request model cho API BorrowSNs
    /// 
    /// </summary>
    /// 

    public class BorrowSNListRequest
    {
        public List<string> SerialNumbers { get; set; } // Danh sách Serial Numbers cần cho mượn
        public string Borrower { get; set; } // Tên người mượn
    }
}
