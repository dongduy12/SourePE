using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API_WEB.ModelsDB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API_WEB.ModelsOracle;
using API_WEB.Dtos.PdRepositorys;
using System.Data.SqlClient;
using Oracle.ManagedDataAccess.Client;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Http;


namespace API_WEB.Controllers.PdRepositorys
{
     [Route("[controller]")]
    [ApiController]
    public class DdRepositorysController : ControllerBase
    {
        private readonly CSDL_NE PdStockDb;
        private readonly OracleDbContext R107Db;
        public DdRepositorysController(CSDL_NE PdStockDb, OracleDbContext R107Db)
        {
            this.PdStockDb = PdStockDb;
            this.R107Db = R107Db;
        }

        // Lấy tất cả các sản phẩm
        [HttpGet]
        [Route("GetAll")]
        public IActionResult GetAll()
        {
            var result = PdStockDb.PdStocks.ToList(); 
            if(result == null)
            {
                return NotFound();
            }
            return Ok(new { data = result });
        }

        // Tìm sản phẩm theo danh sách serialNumber

        [HttpPost]
        [Route("GetBySerialNumber")]
        public IActionResult GetBySerialNumber([FromBody] List<string> serialNumbers)
        {
            if (serialNumbers == null || !serialNumbers.Any())
            {
                return BadRequest("Serial numbers list cannot be null or empty.");
            }

            var results = PdStockDb.PdStocks
                .Where(s => serialNumbers.Contains(s.SerialNumber))
                .ToList();

            if (results == null || !results.Any())
            {
                return NotFound();
            }

            return Ok(new { data = results });
        }


        // Tìm sản phẩm theo ModelName

        [HttpPost]
        [Route("GetByModelName")]
        public IActionResult GetByModelName([FromBody] List<string> modelNames)
        {
            if (modelNames == null || !modelNames.Any())
            {
                return BadRequest("Serial numbers list cannot be null or empty.");
            }

            var results = PdStockDb.PdStocks
                .Where(s => modelNames.Contains(s.ModelName))
                .ToList();

            if (results == null || !results.Any())
            {
                return NotFound();
            }

            return Ok(new { data = results });
        }


        // Tìm sản phẩm theo CartonNo
        [HttpPost]
        [Route("GetByCartonNo")]
        public IActionResult GetByCartonNo([FromBody] List<string> CartonNos)
        {
            if (CartonNos == null || !CartonNos.Any())
            {
                return BadRequest("Serial numbers list cannot be null or empty.");
            }

            var results = PdStockDb.PdStocks
                .Where(s => CartonNos.Contains(s.CartonNo))
                .ToList();

            if (results == null || !results.Any())
            {
                return NotFound();
            }

            return Ok(new { data = results });
        }

        // Them san pham vao bang hiện tại


        [HttpPost]
        [Route("PostToTable")]
        public IActionResult PostToTable([FromBody] List<AddPdStockDto> pdStockDtos)
        {
            if (pdStockDtos == null || !pdStockDtos.Any())
            {
                return BadRequest(new { message = "Product list is empty or invalid." });
            }

            try
            {
                var errorMessages = new List<string>();

                foreach (var dto in pdStockDtos)
                {
                    // Kiểm tra dữ liệu không hợp lệ
                    if (string.IsNullOrEmpty(dto.SerialNumber))
                    {
                        errorMessages.Add($"Product with missing SerialNumber: {dto?.ModelName ?? "Unknown Model"}.");
                        continue;
                    }

                    if (string.IsNullOrEmpty(dto.CartonNo))
                    {
                        errorMessages.Add($"Product with SerialNumber {dto.SerialNumber} has a missing CartonNo.");
                        continue;
                    }

                    if (string.IsNullOrEmpty(dto.LocationStock))
                    {
                        errorMessages.Add($"Product with SerialNumber {dto.SerialNumber} has a missing LocationStock.");
                        continue;
                    }

                    if (string.IsNullOrEmpty(dto.EntryOp))
                    {
                        errorMessages.Add($"Product with SerialNumber {dto.SerialNumber} has a missing EntryOp.");
                        continue;
                    }

                    // Kiểm tra xem CartonNo đã tồn tại hay chưa, trừ khi CartonNo là "N/A"
                    if (dto.CartonNo != "N/A")
                    {
                        var existingCarton = PdStockDb.PdStocks.FirstOrDefault(p => p.CartonNo == dto.CartonNo);
                        if (existingCarton != null)
                        {
                            errorMessages.Add($"Product with CartonNo {dto.CartonNo} already exists in stock.");
                            continue;
                        }
                    }

                    // Kiểm tra xem SerialNumber đã tồn tại hay chưa
                    var existingSerial = PdStockDb.PdStocks.FirstOrDefault(p => p.SerialNumber == dto.SerialNumber);
                    if (existingSerial != null)
                    {
                        errorMessages.Add($"Product with SerialNumber {dto.SerialNumber} already exists in stock.");
                        continue;
                    }

                    // Chuyển từ DTO sang Entity
                    var pdStock = new PdStock
                    {
                        SerialNumber = dto.SerialNumber,
                        ModelName = dto.ModelName,
                        CartonNo = dto.CartonNo,
                        LocationStock = dto.LocationStock,
                        EntryDate = DateTime.Now,
                        EntryOp = dto.EntryOp
                    };

                    // Thêm sản phẩm vào database
                    PdStockDb.PdStocks.Add(pdStock);
                }

                // Lưu thay đổi vào database
                PdStockDb.SaveChanges();

                // Phản hồi nếu có lỗi
                if (errorMessages.Any())
                {
                    return Ok(new
                    {
                        message = "Some products were not processed due to errors.",
                        errors = errorMessages
                    });
                }

                // Phản hồi thành công
                return Ok(new { message = "All products were added successfully." });
            }
            catch (Exception ex)
            {
                // Log chi tiết lỗi
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }

                return StatusCode(500, new
                {
                    message = $"Internal server error: {ex.Message}",
                    details = ex.InnerException?.Message
                });
            }
        }





        //============== xóa sản phẩm khỏi kho hiện tại==========


        [HttpDelete]
        [Route("DeleteBySerialNumbers")]
        public IActionResult DeleteBySerialNumbers([FromBody] List<DeletePdStockDto> deletePdStockDtos)
        {
            if (deletePdStockDtos == null || !deletePdStockDtos.Any())
            {
                return BadRequest("Invalid or empty product data.");
            }

            try
            {
                var errorMessages = new List<string>();
                var notFoundSerialNumbers = new List<string>();

                foreach (var deletePdStockDto in deletePdStockDtos)
                {
                    // Kiểm tra dữ liệu không hợp lệ
                    if (string.IsNullOrEmpty(deletePdStockDto.SerialNumber))
                    {
                        errorMessages.Add($"Invalid data for SerialNumber: {deletePdStockDto.SerialNumber ?? "Unknown"}.");
                        continue;
                    }

                    var product = PdStockDb.PdStocks.FirstOrDefault(p => p.SerialNumber == deletePdStockDto.SerialNumber);
                    if (product == null)
                    {
                        notFoundSerialNumbers.Add(deletePdStockDto.SerialNumber);
                        continue;
                    }

                    // Lưu dữ liệu sản phẩm vào bảng PdStockHistory
                    var productHistory = new PdStockHistory
                    {
                        SerialNumber = product.SerialNumber,
                        ModelName = product.ModelName,
                        CartonNo = product.CartonNo,
                        LocationStock = product.LocationStock,
                        EntryDate = product.EntryDate,
                        EntryOp = product.EntryOp,
                        OutDate = DateTime.Now,
                        OutOp = deletePdStockDto.OutOp
                    };

                    PdStockDb.PdStockHistories.Add(productHistory);

                    // Xóa sản phẩm khỏi bảng PdStock
                    PdStockDb.PdStocks.Remove(product);
                }

                // Lưu các thay đổi vào database
                PdStockDb.SaveChanges();

                // Phản hồi nếu có lỗi
                if (notFoundSerialNumbers.Any() || errorMessages.Any())
                {
                    return Ok(new
                    {
                        message = "Some products could not be processed.",
                        notFoundSerialNumbers = notFoundSerialNumbers,
                        errors = errorMessages
                    });
                }

                return Ok(new { message = "Tất cả sản phẩm được xóa thành công. " });
            }

            catch (Exception ex)
            {
                // Log chi tiết lỗi
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return StatusCode(500, $"Internal server error: {ex.Message}. Details: {ex.InnerException?.Message}");
            }
        }



        // Tìm lịch sử sản phẩm theo serialNumber
        [HttpPost]
        [Route("GetHistoryBySerialNumber")]
        public IActionResult GetHistoryBySerialNumber([FromBody] List<string> SerialNumber)
        {
            if (SerialNumber == null || !SerialNumber.Any())
            {
                return BadRequest("Serial numbers list cannot be null or empty.");
            }
            var results = PdStockDb.PdStockHistories.Where(s => SerialNumber.Contains(s.SerialNumber)).ToList();

            if (results == null || !results.Any())
            {
                return NotFound();
            }

            return Ok(new { data = results });
        }

        // Tìm sản phẩm theo mã CARTON_NO trong bảng R107
        [HttpPost]
        [Route("GetR107byCartonNos")]
        public async Task<IActionResult> GetR107byCartonNo([FromBody] List<string> cartonNos)
        {
            if (cartonNos == null || cartonNos.Count == 0)
            {
                return BadRequest("The cartonNo list is required and cannot be empty.");
            }
            var result = await R107Db.OracleDataR107PdStock
                .Where(p => cartonNos.Contains(p.CARTON_NO))
                .Select(p => new
                {
                    p.CARTON_NO,
                    p.SERIAL_NUMBER,
                    p.MODEL_NAME
                })

                .ToListAsync();

            if (result == null || result.Count == 0)
            {
                return NotFound("No products found with the given CARTON_NO.");
            }

            return Ok(new { data = result });
        }

        // tìm sản phẩm nhập kho theo khoảng thời gian

        [HttpPost]
        [Route("GetProductsByDateRange")]
        public IActionResult GetProductsByDateRange([FromBody] DateRangeDto dateRangeDto)
        {
            if (dateRangeDto == null || dateRangeDto.StartDate == default || dateRangeDto.EndDate == default)
            {
                return BadRequest(new { message = "Invalid date range provided." });
            }

            try
            {
                // Lấy dữ liệu từ bảng PdStock
                var pdStockProducts = PdStockDb.PdStocks
                    .Where(p => p.EntryDate >= dateRangeDto.StartDate && p.EntryDate <= dateRangeDto.EndDate)
                    .Select(p => new
                    {
                        p.SerialNumber,
                        p.ModelName,
                        p.CartonNo,
                        p.LocationStock,
                        p.EntryOp,
                        EntryDate = p.EntryDate,
                        Action = "In Stock"
                    }).ToList();

                // Lấy dữ liệu từ bảng PdStockHistory
                var pdStockHistoryProducts = PdStockDb.PdStockHistories
                    .Where(h => h.EntryDate >= dateRangeDto.StartDate && h.EntryDate <= dateRangeDto.EndDate)
                    .Select(h => new
                    {
                        h.SerialNumber,
                        h.ModelName,
                        h.CartonNo,
                        h.LocationStock,
                        h.EntryOp,
                        EntryDate = h.EntryDate,
                        Action = "History"
                    }).ToList();

                // Gộp dữ liệu từ hai bảng
                var allProducts = pdStockProducts.Concat(pdStockHistoryProducts).ToList();

                // Trả về dữ liệu
                return Ok(new
                {
                    message = "Products retrieved successfully.",
                    data = allProducts
                });
            }
            catch (Exception ex)
            {
                // Log chi tiết lỗi
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }

                return StatusCode(500, new
                {
                    message = $"Internal server error: {ex.Message}",
                    details = ex.InnerException?.Message
                });
            }
        }

        // tìm sản phẩm xuất kho theo khoảng thời gian

        [HttpPost]
        [Route("GetExportedProductsByDateRange")]
        public IActionResult GetExportedProductsByDateRange([FromBody] DateRangeDto dateRangeDto)
        {
            if (dateRangeDto == null || dateRangeDto.StartDate == default || dateRangeDto.EndDate == default)
            {
                return BadRequest(new { message = "Invalid date range provided." });
            }

            try
            {
                // Lấy dữ liệu từ bảng PdStockHistory dựa trên OutDate
                var exportedProducts = PdStockDb.PdStockHistories
                    .Where(h => h.OutDate >= dateRangeDto.StartDate && h.OutDate <= dateRangeDto.EndDate)
                    .Select(h => new
                    {
                        h.SerialNumber,
                        h.ModelName,
                        h.CartonNo,
                        h.LocationStock,
                        h.EntryDate,
                        h.EntryOp,
                        h.OutDate,
                        h.OutOp
                    }).ToList();

                // Trả về dữ liệu
                return Ok(new
                {
                    message = "Exported products retrieved successfully.",
                    data = exportedProducts
                });
            }
            catch (Exception ex)
            {
                // Log chi tiết lỗi
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }

                return StatusCode(500, new
                {
                    message = $"Internal server error: {ex.Message}",
                    details = ex.InnerException?.Message
                });
            }
        }

        // Tìm sản phẩm theo mã SERIAL_NUMBER trong bảng R107
        [HttpPost]
        [Route("GetR107bySN")]
        public async Task<IActionResult> GetR107bySN([FromBody] List<string> serialNumbers)
        {
            if (serialNumbers == null || serialNumbers.Count == 0)
            {
                return BadRequest("The serial number list is required and cannot be empty.");
            }

            var result = await R107Db.OracleDataR107PdStock
                .Where(p => serialNumbers.Contains(p.SERIAL_NUMBER))
                .Select(p => new
                {
                    p.CARTON_NO,
                    p.SERIAL_NUMBER,
                    p.MODEL_NAME
                })
                .ToListAsync();

            if (result == null || result.Count == 0)
            {
                return NotFound("No products found with the given SERIAL_NUMBER.");
            }

            return Ok(new { data = result });
        }

    }
}