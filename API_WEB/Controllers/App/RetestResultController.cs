using API_WEB.ModelsDB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Net.Http;

namespace API_WEB.Controllers.App
{

    [Route("api/[controller]")]
    [ApiController]
    public class RetestResultController : Controller
    {
        private readonly CSDL_NE _sqlContext;
        private readonly HttpClient _httpClient;
        private readonly string _oracleApiUrl;
        public RetestResultController(CSDL_NE sqlContext, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _sqlContext = sqlContext;
            _httpClient = httpClientFactory.CreateClient();
            _oracleApiUrl = "http://10.220.130.119:9090/api/RepairStatus/repair-status";
        }

        [HttpPost("submit-with-img")]
        public async Task<IActionResult> SubmitRetest([FromForm] string serialNumber, [FromForm] string status, [FromForm] string employeeId, [FromForm] string? notes, [FromForm] string handOverStatus, [FromForm] IFormFile? image)
        {
            //if(string.IsNullOrEmpty(serialNumber) || string.IsNullOrEmpty(status) || (status != "Pass" && status != "Fail"))
            //{
            //    return BadRequest("Invalid SerialNumber or Status");
            //}
            var payload = new
            {
                serialNumbers = serialNumber,
                type = "RETEST",
                status = status,
                employeeId = employeeId,
                tag = "Confirm",
                notes = notes,
                handOverStatus = handOverStatus
            };

            var oracleResponse = await _httpClient.PostAsJsonAsync(_oracleApiUrl, payload);
            if (!oracleResponse.IsSuccessStatusCode)
            {
                return StatusCode((int)oracleResponse.StatusCode, "Fail to update Oracle");
            }

            //Luu anh va thong tin vao sql Server
            string? imagePath = null;
            if(image != null)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
                // Tạo thư mục Uploads nếu chưa tồn tại
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"{Guid.NewGuid()}_{image.FileName}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }
                imagePath = $"/Uploads/{fileName}";
            }
            var retestResult = new RetestResult
            {
                SerialNumber = serialNumber,
                ImagePath = imagePath,
                CreatedAt = DateTime.UtcNow
            };

            _sqlContext.RetestResult.Add(retestResult);
            await _sqlContext.SaveChangesAsync();
            return Ok(new { message = "Retest result saved successfully." });
        }


        [HttpGet("get-result/{serialNumber}")]
        public async Task<IActionResult> GetRetestResult(string serialNumber)
        {
            var result = await _sqlContext.RetestResult.FirstOrDefaultAsync(r => r.SerialNumber == serialNumber);
            if(result == null)
            {
                return NotFound("no result found for this serial number!");
            }
            return Ok(result);
        }
    }
}

