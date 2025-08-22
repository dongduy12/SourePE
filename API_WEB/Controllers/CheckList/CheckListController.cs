using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YourNamespace.Models;
using API_WEB.ModelsDB;
using API_WEB.ModelsOracle;
using System.Collections.Generic;
using DocumentFormat.OpenXml.Spreadsheet;

namespace API_WEB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CheckListController : ControllerBase
    {
        private readonly CSDL_NE _sqlContext;
        private readonly IWebHostEnvironment _environment;
        private readonly OracleDbContext _oracleContext;

        private readonly CSDL_NE _context;

        public CheckListController(CSDL_NE sqlContext, IWebHostEnvironment environment, OracleDbContext oracleContext, CSDL_NE context)
        {
            _sqlContext = sqlContext;
            _environment = environment;
            _oracleContext = oracleContext;
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        



        // POST: api/CheckList
        [HttpPost("create-new-issue")]
        public async Task<IActionResult> CreateIssue([FromForm] CheckListRequest model)
        {
            try
            {
                if (model == null)
                {
                    return BadRequest("Invalid data.");
                }

                // Tạo ID dựa trên thời gian tạo (yyyyMMddHHmmss)
                string issueId = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

                // Đường dẫn thư mục lưu ảnh
                string imageDirectory = @"D:\ChecklistPicture";
                if (!Directory.Exists(imageDirectory))
                {
                    Directory.CreateDirectory(imageDirectory);
                }

                // Xử lý file ảnh nếu có
                string filePath = null;
                if (model.Picture != null)
                {
                    string fileName = $"{issueId}{Path.GetExtension(model.Picture.FileName)}";
                    filePath = Path.Combine(imageDirectory, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.Picture.CopyToAsync(stream);
                    }
                }

                // Tính DFR = (NGqty / Input) * 100, giữ 2 chữ số thập phân
                string dfrValue = (model.Input > 0) ? ((double)model.NGQty / model.Input * 100).ToString("0.00") + "%" : "0.00%";


                // Tạo đối tượng CheckList
                var newIssue = new CheckList
                {
                    ID = issueId, // Gán ID là chuỗi yyyyMMddHHmmss
                    Publisher = model.Publisher,
                    Time = model.Time,
                    Project = model.Project,
                    ModelName = model.ModelName,
                    Station = model.Station,
                    Error = model.Error,
                    Input = model.Input,
                    NGQty = model.NGQty,
                    DFR = dfrValue,  // Lưu giá trị DFR đã tính
                    RootCause = model.RootCause,
                    Location = model.Location ?? "", // Nếu null thì lưu là chuỗi rỗng
                    Status = model.Status,
                    Owner = model.Owner,
                    LinkPicture = filePath ?? "" // Lưu đường dẫn ảnh vào database, Nếu không có ảnh, lưu là chuỗi rỗng
                };

                _sqlContext.CheckLists.Add(newIssue);
                await _sqlContext.SaveChangesAsync();

                return Ok(new { Message = "Issue created successfully", id = newIssue.ID });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // API GET: Lấy danh sách PRODUCT_LINE không trùng lặp (Loại bỏ NULL)
        [HttpGet("get-product-lines")]
        public async Task<IActionResult> GetDistinctProductLines()
        {
            try
            {
                var productLines = await _oracleContext.OracleDataCModelDesc
                    .FromSqlRaw(@"SELECT DISTINCT COALESCE(PRODUCT_LINE, 'UNKNOWN') AS PRODUCT_LINE 
                                  FROM SFIS1.C_MODEL_DESC_T 
                                  WHERE MODEL_SERIAL='ADAPTER' AND PRODUCT_LINE IS NOT NULL 
                                  ORDER BY PRODUCT_LINE")
                    .Select(x => x.PRODUCT_LINE)
                    .ToListAsync();

                return Ok(productLines);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // API POST: Lấy danh sách MODEL_NAME không trùng lặp theo PRODUCT_LINE
        [HttpPost("get-model-names")]
        public async Task<IActionResult> GetDistinctModelNames([FromBody] ProductLineRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.ProductLine))
                {
                    return BadRequest(new { message = "Product Line cannot be empty." });
                }

                var modelNames = await _oracleContext.OracleDataCModelDesc
                    .FromSqlRaw(@"SELECT DISTINCT MODEL_NAME 
                                  FROM SFIS1.C_MODEL_DESC_T 
                                  WHERE PRODUCT_LINE = {0} 
                                  AND MODEL_NAME IS NOT NULL 
                                  ORDER BY MODEL_NAME", request.ProductLine)
                    .Select(x => x.MODEL_NAME)
                    .ToListAsync();

                if (modelNames.Count == 0)
                {
                    return NotFound(new { message = $"No MODEL_NAME found for Product Line: {request.ProductLine}" });
                }

                return Ok(modelNames);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        // API lấy tất cả thông tin check list

        [HttpPost("get-checklist")]
        public async Task<IActionResult> GetCheckList([FromBody] CheckListRequestModel request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.ID))
                {
                    return BadRequest(new { message = "ID cannot be empty." });
                }

                if (request.ID.ToUpper() == "ALL")
                {
                    // Trả về tất cả dữ liệu, xử lý NULL
                    var allData = await _sqlContext.CheckLists
                        .Select(c => new
                        {
                            ID = c.ID ?? "",
                            Publisher = c.Publisher ?? "",
                            Time = c.Time,
                            Project = c.Project ?? "",
                            ModelName = c.ModelName ?? "",
                            Station = c.Station ?? "",
                            Error = c.Error ?? "",
                            Input = c.Input,
                            NGQty = c.NGQty,
                            DFR = c.DFR ?? "",
                            RootCause = c.RootCause ?? "",
                            Location = c.Location ?? "",
                            FA = c.FA ?? "",  // Thêm FA
                            Action = c.Action ?? "",  // Thêm Action
                            Status = c.Status ?? "",
                            Owner = c.Owner ?? "",
                            LinkPicture = c.LinkPicture ?? ""
                        })
                        .ToListAsync();

                    return Ok(allData);
                }
                else
                {
                    // Trả về dữ liệu theo ID, xử lý NULL
                    var checkListData = await _sqlContext.CheckLists
                        .Where(c => c.ID == request.ID)
                        .Select(c => new
                        {
                            ID = c.ID ?? "",
                            Publisher = c.Publisher ?? "",
                            Time = c.Time,
                            Project = c.Project ?? "",
                            ModelName = c.ModelName ?? "",
                            Station = c.Station ?? "",
                            Error = c.Error ?? "",
                            Input = c.Input,
                            NGQty = c.NGQty,
                            DFR = c.DFR ?? "",
                            RootCause = c.RootCause ?? "",
                            Location = c.Location ?? "",
                            FA = c.FA ?? "",  // Thêm FA
                            Action = c.Action ?? "",  // Thêm Action
                            Status = c.Status ?? "",
                            Owner = c.Owner ?? "",
                            LinkPicture = c.LinkPicture ?? ""
                        })
                        .FirstOrDefaultAsync();

                    if (checkListData == null)
                    {
                        return NotFound(new { message = $"No data found for ID: {request.ID}" });
                    }

                    return Ok(checkListData);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        // API update issue
        // API update issue
        [HttpPost("update-issue")]
        public async Task<IActionResult> UpdateIssue([FromBody] UpdateIssueRequest request)
        {
            if (string.IsNullOrEmpty(request.ID))
            {
                return BadRequest(new { message = "Issue ID is required." });
            }

            var issue = await _sqlContext.CheckLists.FirstOrDefaultAsync(c => c.ID == request.ID);

            if (issue == null)
            {
                return NotFound(new { message = "Issue not found." });
            }

            // Cập nhật các trường thông tin
            issue.Status = request.Status ?? issue.Status;
            issue.FA = request.FA ?? issue.FA;
            issue.Action = request.Action ?? issue.Action;

            // Lưu các thay đổi vào cơ sở dữ liệu
            _sqlContext.CheckLists.Update(issue);
            await _sqlContext.SaveChangesAsync();

            return Ok(new { message = "Issue updated successfully!", id = issue.ID });
        }


        // API delete issue
        [HttpDelete("delete-issue/{id}")]
        public async Task<IActionResult> DeleteIssue(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest(new { message = "Issue ID is required." });
            }

            var issue = await _sqlContext.CheckLists.FirstOrDefaultAsync(c => c.ID == id);

            if (issue == null)
            {
                return NotFound(new { message = $"Issue with ID {id} not found." });
            }

            try
            {
                // Xóa hình ảnh khỏi thư mục nếu tồn tại
                if (!string.IsNullOrEmpty(issue.LinkPicture) && System.IO.File.Exists(issue.LinkPicture))
                {
                    System.IO.File.Delete(issue.LinkPicture);
                }

                // Xóa issue khỏi cơ sở dữ liệu
                _sqlContext.CheckLists.Remove(issue);
                await _sqlContext.SaveChangesAsync();

                return Ok(new { message = $"Issue with ID {id} has been deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        //API lấy Department theo UserId

        // API GET: Lấy Department dựa trên Username
        // API GET: Lấy Department dựa trên Username
        [HttpGet("check-department/{username}")]
        public async Task<IActionResult> CheckDepartment(string username)
        {
            try
            {
                var user = await _sqlContext.Users
                    .Where(u => u.Username == username)
                    .Select(u => new { Username = u.Username.Trim(), Department = u.Department.Trim() })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound(new { message = $"User with username '{username}' not found." });
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        // API gửi mail 
        [HttpPost("send-daily-alerts")]
        public async Task<IActionResult> SendDailyAlerts()
        {
            try
            {
                // Lấy danh sách các issue chưa đóng (Status khác "Close")
                var issues = await _sqlContext.CheckLists
                .Where(c => c.Status != "Close")
                .Select(c => new IssueInfo  // ✅ Chỉ định kiểu IssueInfo
                {
                    ID = c.ID,
                    Owner = c.Owner,
                    Project = c.Project,
                    ModelName = c.ModelName,
                    Station = c.Station,
                    Error = c.Error,
                    RootCause = c.RootCause,
                    Location = c.Location,
                    Status = c.Status
                })
                .ToListAsync();

                if (!issues.Any())
                {
                    return Ok("No issues need alerts.");
                }

                // Nhóm các issue theo Owner (Sử dụng IssueInfo thay vì anonymous type)
                var ownerGroups = issues.GroupBy(i => i.Owner).ToList();

                // Lấy danh sách email của các Owner
                var users = await _sqlContext.Users
                    .Where(u => ownerGroups.Select(g => g.Key).Contains(u.Department))
                    .Select(u => new UserInfo { Department = u.Department, Email = u.Email })
                    .ToListAsync();

                // Gửi email cho từng nhóm Owner
                foreach (var group in ownerGroups)
                {
                    var ownerName = group.Key; // Tên Owner (VD: "ME-SMT")
                    var emails = users
                    .Where(u => u.Department.Trim().ToLower() == ownerName.Trim().ToLower()) // So sánh với Department
                    .Select(u => u.Email)
                    .ToList();


                    Console.WriteLine($"===== Kiểm tra Users trước khi lọc email =====");
                    foreach (var user in users)
                    {
                        Console.WriteLine($"User: {user.Department} - Department: {user.Department} - Email: {user.Email}");
                    }
                    Console.WriteLine("========================================");

                    // Kiểm tra email trước khi gửi
                    Console.WriteLine($"Gửi email đến: {string.Join(",", emails)}");
                    if (!emails.Any())
                    {
                        Console.WriteLine($"❌ Không tìm thấy email nào cho Department: {ownerName}");
                        continue;
                    }

                    // Kiểm tra dữ liệu trước khi gửi email
                    //Console.WriteLine($"Gửi email đến: {string.Join(",", emails)}");
                    //Console.WriteLine($"Chủ sở hữu: {ownerName}");
                    //Console.WriteLine($"Nội dung email: {emailBody}");
                    //await MailHelper.SendEmailAsync("jax.fw.ruan@mail.foxconn.com,mbd-vn-pe-nvidia@mail.foxconn.com,mark.ds.ruan@mail.foxconn.com", "Test Email", "Jax test he thong");
                    if (emails.Any()) // Nếu có email thì mới gửi
                    {
                        // Kiểm tra nếu danh sách email rỗng, bỏ qua Owner này
                        if (!emails.Any())
                        {
                            Console.WriteLine($"Không tìm thấy email nào cho Owner: {ownerName}");
                            continue;
                        }

                        // Tạo nội dung email
                        string emailBody = $"<h3>Issue list not close yet from {ownerName}:</h3><ul>";
                        foreach (var issue in group)
                        {
                            emailBody += $"<li><b>ID:</b> {issue.ID}, <b>Project:</b> {issue.Project}, <b>Model:</b> {issue.ModelName}, <b>Station:</b> {issue.Station}, <b>Error:</b> {issue.Error}, <b>RootCause:</b> {issue.RootCause}, <b>Location:</b> {issue.Location}, <b>Status:</b> {issue.Status}</li>";
                        }
                        emailBody += "</ul>";
                        emailBody += $"<h3>Please sign in to address http://10.220.130.119:8000/CheckList to update</h3>";
                        emailBody += $"<h5>This is auto mail, please don't reply! </h5>";


                        // Gửi email
                        await MailHelper.SendEmailAsync(string.Join(",", emails), $"Notice issue list not close yet from {ownerName}", emailBody);
                    }
                }

                return Ok("Emails sent successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }



    }

    // Model cho API request
    public class CheckListRequest
    {
        public string Publisher { get; set; }
        public DateTime Time { get; set; }
        public string Project { get; set; }
        public string ModelName { get; set; }
        public string Station { get; set; }
        public string Error { get; set; }
        public int Input { get; set; }
        public int NGQty { get; set; }
        public string RootCause { get; set; }
        public string? Location { get; set; }
        public string Status { get; set; }  // Cho phép null (Không bắt buộc)
        public string Owner { get; set; }
        public IFormFile? Picture { get; set; } // Cho phép null (Không bắt buộc)
    }

    // Model cho API POST
    public class ProductLineRequest
    {
        public string ProductLine { get; set; }
    }

    // Model nhận request từ API
    public class CheckListRequestModel
    {
        public string ID { get; set; }
    }

    // Model update issue
    public class UpdateIssueRequest
    {
        public string ID { get; set; }
        public string? Status { get; set; }
        public string? FA { get; set; }
        public string? Action { get; set; }
    }

    public class IssueInfo
    {
        public string ID { get; set; }
        public string Owner { get; set; }
        public string Project { get; set; }
        public string ModelName { get; set; }
        public string Station { get; set; }
        public string Status { get; set; }
        public string Error { get; set; }
        public string RootCause { get; set; }
        public string Location { get; set; }
    }

    public class UserInfo
    {
        public string Department { get; set; }
        public string Email { get; set; }
    }
}
