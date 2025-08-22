using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API_WEB.ModelsDB;
using API_WEB.ModelsOracle;
using System.Net.Http;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Oracle.ManagedDataAccess.Client;

namespace API_WEB.Controllers.Scrap
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScrapController : ControllerBase
    {
        private readonly CSDL_NE _sqlContext;
        private readonly OracleDbContext _oracleContext;
        private readonly HttpClient _httpClient;

        public ScrapController(CSDL_NE sqlContext, OracleDbContext oracleContext, IHttpClientFactory httpClientFactory)
        {
            _sqlContext = sqlContext;
            _oracleContext = oracleContext;

            // Cấu hình HttpClient với HttpClientHandler để bỏ qua chứng chỉ SSL (chỉ dùng trong môi trường test)
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true // Bỏ qua kiểm tra chứng chỉ
            };
            _httpClient = new HttpClient(handler);
            _httpClient.BaseAddress = new Uri("https://10.220.130.217:443/SfcSmartRepair/");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // API INPUT-SN
        [HttpPost("input-sn")]
        public async Task<IActionResult> InputSN([FromBody] InputSNRequest request)
        {
            try
            {
                // Kiểm tra dữ liệu đầu vào
                if (request == null || request.SNs == null || !request.SNs.Any())
                {
                    return BadRequest(new { message = "Danh sách SN không được để trống." });
                }

                if (string.IsNullOrEmpty(request.CreatedBy) || string.IsNullOrEmpty(request.ApproveScrapPerson))
                {
                    return BadRequest(new { message = "CreatedBy và ApproveScrapPerson không được để trống." });
                }

                // Kiểm tra Purpose hợp lệ
                if (string.IsNullOrEmpty(request.Purpose) || (request.Purpose != "0" && request.Purpose != "1" && request.Purpose != "2" && request.Purpose != "3" && request.Purpose != "4"))
                {
                    return BadRequest(new { message = "Purpose phải là '0','1','2','3' hoặc '4'." });
                }

                // Kiểm tra độ dài các trường
                if (request.SNs.Any(sn => sn.Length > 50))
                {
                    return BadRequest(new { message = "SN không được dài quá 50 ký tự." });
                }

                if (request.CreatedBy.Length > 50 || request.ApproveScrapPerson.Length > 50 || (request.Description != null && request.Description.Length > 50))
                {
                    return BadRequest(new { message = "CreatedBy, Description, và ApproveScrapPerson không được dài quá 50 ký tự." });
                }

                // Kiểm tra danh sách SN trong bảng ScrapList
                var existingSNs = await _sqlContext.ScrapLists
                    .Where(s => request.SNs.Contains(s.SN))
                    .ToListAsync();

                // Tìm các SN không tồn tại trong bảng ScrapList
                var nonExistingSNs = request.SNs.Except(existingSNs.Select(s => s.SN)).ToList();
                if (nonExistingSNs.Any())
                {
                    return BadRequest(new { message = $"Các SN sau không tồn tại trong bảng ScrapList: {string.Join(", ", nonExistingSNs)}" });
                }

                // Kiểm tra trạng thái ApplyTaskStatus của các SN
                var rejectedSNs = new List<string>();
                var validSNs = new List<ScrapList>();

                foreach (var sn in existingSNs)
                {
                    if (sn.ApplyTaskStatus == 0)
                    {
                        rejectedSNs.Add($"{sn.SN} (SPE đã đồng ý phế, chờ xin task)");
                    }
                    else if (sn.ApplyTaskStatus == 1)
                    {
                        rejectedSNs.Add($"{sn.SN} (đang xin task, chờ NV gửi task)");
                    }
                    else if (sn.ApplyTaskStatus == 3)
                    {
                        rejectedSNs.Add($"{sn.SN} (NV đã approved thay BGA)");
                    }
                    else if (sn.ApplyTaskStatus == 5)
                    {
                        rejectedSNs.Add($"{sn.SN} (Đã có task, chờ chuyển MRB)");
                    }
                    else if (sn.ApplyTaskStatus == 6)
                    {
                        rejectedSNs.Add($"{sn.SN} (Đã chuyển kho phế, chờ MRB xác nhận)");
                    }
                    else if (sn.ApplyTaskStatus == 7)
                    {
                        rejectedSNs.Add($"{sn.SN} (Đã chuyển kho phế thành công)");
                    }
                    else if (sn.ApplyTaskStatus == 2 || sn.ApplyTaskStatus == 4 )
                    {
                        validSNs.Add(sn); // SN hợp lệ để cập nhật
                    }
                    else
                    {
                        rejectedSNs.Add($"{sn.SN} (trạng thái không xác định: {sn.ApplyTaskStatus})");
                    }
                }

                if (rejectedSNs.Any())
                {
                    return BadRequest(new { message = $"Các SN sau không hợp lệ để cập nhật: {string.Join(", ", rejectedSNs)}" });
                }

                if (!validSNs.Any())
                {
                    return BadRequest(new { message = "Không có SN nào hợp lệ để cập nhật (yêu cầu ApplyTaskStatus = 2 & 4)." });
                }

                // kiểm tra xem có lẫn lộn bản giữa Bonepile 1.0 và SN 2.0 không
                var remarkGroups = existingSNs
                .Where(s => s.Remark != null) // Chỉ xét các SN có Remark không null
                .GroupBy(s => s.Remark)
                .Select(g => new { Remark = g.Key!, SNs = g.Select(s => s.SN).ToList() })
                .ToList();

                if (remarkGroups.Count > 1)
                {
                    var errorMessage = "Danh sách SN có trạng thái cả Bonpile 1.0 và Bonpile 2.0:\n" +
                        string.Join("\n", remarkGroups.Select(g => $"Remark '{g.Remark}': {string.Join(", ", g.SNs)}"));
                    return BadRequest(new { message = errorMessage });
                }


                // Tạo danh sách SN để gửi đến API bên thứ ba
                var serialNumbers = string.Join(",", request.SNs);
                Console.WriteLine($"Sending t_serial_numbers to external API: {serialNumbers}");

                // Gọi API bên thứ ba để lấy dữ liệu AfterBeforeKanban
                var externalRequest = new
                {
                    type = "task_system",
                    t_serial_numbers = serialNumbers
                };

                HttpResponseMessage externalResponse;
                List<ExternalApiResponse> externalData = null;

                try
                {
                    externalResponse = await _httpClient.PostAsJsonAsync("api/query", externalRequest);
                    externalResponse.EnsureSuccessStatusCode();
                    var responseContent = await externalResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"External API response: {responseContent}");
                    externalData = JsonSerializer.Deserialize<List<ExternalApiResponse>>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true // Bỏ qua phân biệt hoa thường
                    });
                }
                catch (HttpRequestException ex)
                {
                    // Log chi tiết lỗi khi gọi API bên thứ ba
                    return StatusCode(500, new { message = "Không thể kết nối đến API bên thứ ba.", error = ex.Message, innerException = ex.InnerException?.Message });
                }

                // Xác định giá trị cho Purpose, Category, và Sloc dựa trên đầu vào Purpose
                string purposeValue;
                switch (request.Purpose)
                {
                    case "0":
                        purposeValue = "SPE approve to scrap";
                        break;
                    case "1":
                        purposeValue = "Scrap to quarterly";
                        break;
                    case "2":
                        purposeValue = "Approved to engineer sample";
                        break;
                    case "3":
                        purposeValue = "Approved to master board";
                        break;
                    case "4":
                        purposeValue = "SPE approve to BGA";
                        break;
                    default:
                        purposeValue = "Unknown"; // Giá trị mặc định nếu Purpose không hợp lệ
                        break;
                }

                string categoryValue;
                switch (request.Purpose)
                {
                    case "0":
                    case "1":
                        categoryValue = "Scrap";
                        break;
                    case "2":
                        categoryValue = "Engineer sample"; // Sửa lỗi chính tả từ "Enginerr sample"
                        break;
                    case "3":
                        categoryValue = "Master board";
                        break;
                    case "4":
                        categoryValue = "BGA";
                        break;
                    default:
                        categoryValue = "Unknown"; // Giá trị mặc định nếu Purpose không hợp lệ
                        break;
                }

                string slocValue;
                switch (request.Purpose)
                {
                    case "0":
                    case "1":
                    case "2":
                    case "3":
                        slocValue = "FXVZ";
                        break;
                    case "4":
                        slocValue = "FXV7"; // Sửa lỗi chính tả từ "Enginerr sample"
                        break;
                    default:
                        slocValue = "Unknown"; // Giá trị mặc định nếu Purpose không hợp lệ
                        break;
                }

                int ApplyTaskStatusValue;
                switch (request.Purpose)
                {
                    case "0":
                    case "1":
                    case "2":
                    case "3":
                        ApplyTaskStatusValue = 0;
                        break;
                    case "4":
                        ApplyTaskStatusValue = 3; // Sửa lỗi chính tả từ "Enginerr sample"
                        break;
                    default:
                        ApplyTaskStatusValue = 0; // Giá trị mặc định nếu Purpose không hợp lệ
                        break;
                }
                // Tạo InternalTask mới (đảm bảo không trùng)
                string newInternalTask = await GenerateUniqueInternalTask();

                // Lưu ý các SN không tìm thấy dữ liệu từ API bên thứ ba
                var unmatchedSNs = new List<string>();

                // Cập nhật các bản ghi hợp lệ trong bảng ScrapList
                foreach (var scrapEntry in validSNs)
                {
                    // Tìm dữ liệu từ API bên thứ ba tương ứng với SN
                    var normalizedSN = scrapEntry.SN?.Trim();
                    var externalInfo = externalData?.FirstOrDefault(e =>
                    {
                        var normalizedBoardSN = e.BoardSN?.Trim();
                        Console.WriteLine($"Comparing: sn='{normalizedSN}', external.BoardSN='{normalizedBoardSN}'");
                        return normalizedBoardSN == normalizedSN;
                    });

                    string kanBanStatus = "unknown"; // Giá trị mặc định nếu không tìm thấy dữ liệu
                    if (externalInfo == null)
                    {
                        Console.WriteLine($"No matching data found for SN: {normalizedSN}");
                        unmatchedSNs.Add(normalizedSN);
                    }
                    else
                    {
                        kanBanStatus = externalInfo?.AfterBeforeKanban ?? "unknown";
                        Console.WriteLine($"Matched data for SN: {normalizedSN}, KanBanStatus: {kanBanStatus}");
                    }

                    // Cập nhật các trường của bản ghi
                    scrapEntry.KanBanStatus = kanBanStatus; // Lấy từ API bên thứ ba
                    scrapEntry.Sloc = slocValue; // Dựa trên Purpose
                    scrapEntry.TaskNumber = null; // Để trống
                    scrapEntry.PO = null; // Để trống
                    scrapEntry.Cost = ""; // Không cho phép NULL, để trống dưới dạng chuỗi rỗng
                    //scrapEntry.Remark = null; // Để trống
                    scrapEntry.CreatedBy = request.CreatedBy;
                    scrapEntry.Desc = request.Description ?? ""; // Không cho phép NULL, dùng chuỗi rỗng nếu Description là null
                    scrapEntry.CreateTime = DateTime.Now; // Thời gian hiện tại
                    scrapEntry.ApplyTime = DateTime.Now; // Giá trị ban đầu cho ApplyTime
                    scrapEntry.ApproveScrapperson = request.ApproveScrapPerson;
                    scrapEntry.ApplyTaskStatus = ApplyTaskStatusValue;
                    scrapEntry.FindBoardStatus = "chưa tìm thấy"; // Mặc định
                    scrapEntry.InternalTask = newInternalTask; // Gán InternalTask chung cho tất cả SN
                    scrapEntry.Purpose = purposeValue; // Dựa trên Purpose
                    scrapEntry.Category = categoryValue; // Dựa trên Purpose
                }

                // Thông báo nếu có SN không tìm thấy dữ liệu từ API bên thứ ba
                string message = "Cập nhật danh sách SN thành công.";
                if (unmatchedSNs.Any())
                {
                    message += $" Tuy nhiên, không tìm thấy dữ liệu từ API bên thứ ba cho các SN: {string.Join(", ", unmatchedSNs)}";
                }

                // Lưu thay đổi vào bảng ScrapList
                await _sqlContext.SaveChangesAsync();

                return Ok(new { message, internalTask = newInternalTask });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật dữ liệu.", error = ex.Message });
            }
        }

        // Hàm tạo InternalTask mới theo định dạng "Task-{năm + tháng + ngày}-số thứ tự"
        private async Task<string> GenerateUniqueInternalTask()
        {
            try
            {
                // Lấy ngày hiện tại
                var currentDate = DateTime.Now;
                string datePart = currentDate.ToString("yyyyMMdd"); // Định dạng: yyyyMMdd (ví dụ: 20250327)

                // Tạo tiền tố cho InternalTask
                string prefix = $"Task-{datePart}-"; // Ví dụ: Task-20250327-

                // Tìm số thứ tự lớn nhất của InternalTask trong ngày hiện tại
                var existingTasks = await _sqlContext.ScrapLists
                    .Where(s => s.CreateTime.Date == currentDate.Date && s.InternalTask.StartsWith(prefix))
                    .Select(s => s.InternalTask)
                    .ToListAsync();

                int maxSequenceNumber = 0;
                if (existingTasks.Any())
                {
                    // Lấy số thứ tự từ các InternalTask hiện có
                    foreach (var task in existingTasks)
                    {
                        // Tách phần số thứ tự từ InternalTask (ví dụ: Task-20250327-5 -> 5)
                        var sequencePart = task.Substring(prefix.Length); // Lấy phần sau tiền tố
                        if (int.TryParse(sequencePart, out int sequenceNumber))
                        {
                            if (sequenceNumber > maxSequenceNumber)
                            {
                                maxSequenceNumber = sequenceNumber;
                            }
                        }
                    }
                }

                // Tăng số thứ tự lên 1
                int newSequenceNumber = maxSequenceNumber + 1;

                // Tạo InternalTask mới
                string newInternalTask = $"{prefix}{newSequenceNumber}"; // Ví dụ: Task-20250327-5

                // Kiểm tra xem InternalTask đã tồn tại chưa (đề phòng trường hợp bất ngờ)
                bool isUnique = !await _sqlContext.ScrapLists.AnyAsync(s => s.InternalTask == newInternalTask);
                if (!isUnique)
                {
                    // Nếu không unique (rất hiếm xảy ra), thử lại
                    return await GenerateUniqueInternalTask();
                }

                return newInternalTask;
            }
            catch (Exception ex)
            {
                throw new Exception("Đã xảy ra lỗi khi tạo InternalTask mới.", ex);
            }
        }

        // API: Lấy dữ liệu từ ScrapList với ApplyTaskStatus = 0 hoặc 3
        [HttpGet("get-scrap-status-zero")]
        public async Task<IActionResult> GetScrapStatusZero()
        {
            try
            {
                // Lấy dữ liệu từ bảng ScrapList với ApplyTaskStatus = 0 hoặc 3
                var scrapData = await _sqlContext.ScrapLists
                    .Where(s => s.ApplyTaskStatus == 0 || s.ApplyTaskStatus == 3) // Lọc theo ApplyTaskStatus = 0 hoặc 3
                    .GroupBy(s => s.InternalTask) // Nhóm theo InternalTask
                    .Select(g => new
                    {
                        InternalTask = g.Key,
                        Description = g.First().Desc, // Lấy giá trị đầu tiên của Description
                        ApproveScrapPerson = g.First().ApproveScrapperson, // Lấy giá trị đầu tiên
                        KanBanStatus = g.First().KanBanStatus, // Lấy giá trị đầu tiên
                        Category = g.First().Category,
                        Remark = g.First().Remark,
                        CreateTime = g.First().CreateTime.ToString("yyyy-MM-dd"), // Chỉ lấy ngày tháng năm
                        CreateBy = g.First().CreatedBy, // Lấy giá trị đầu tiên
                        ApplyTaskStatus = g.First().ApplyTaskStatus, // Lấy giá trị đầu tiên
                        TotalQty = g.Count() // Đếm số lượng SN trong mỗi InternalTask
                    })
                    .ToListAsync();

                if (!scrapData.Any())
                {
                    return NotFound(new { message = "Không tìm thấy dữ liệu với ApplyTaskStatus = 0 hoặc 3." });
                }

                return Ok(scrapData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy dữ liệu.", error = ex.Message });
            }
        }

        // API: Tạo task và tích hợp dữ liệu từ API bên thứ ba
        [HttpPost("create-task")]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request)
        {
            try
            {
                // Kiểm tra dữ liệu đầu vào
                if (request == null || request.InternalTasks == null || !request.InternalTasks.Any())
                {
                    return BadRequest(new { message = "Danh sách InternalTasks không được để trống." });
                }

                if (string.IsNullOrEmpty(request.SaveApplyStatus) || (request.SaveApplyStatus != "0" && request.SaveApplyStatus != "1"))
                {
                    return BadRequest(new { message = "SaveApplyStatus phải là '0' hoặc '1'." });
                }

                // Lấy tất cả SN từ ScrapList dựa trên InternalTasks
                var scrapRecords = await _sqlContext.ScrapLists
                    .Where(s => request.InternalTasks.Contains(s.InternalTask))
                    .ToListAsync();

                if (!scrapRecords.Any())
                {
                    return NotFound(new { message = "Không tìm thấy dữ liệu cho các InternalTasks được cung cấp." });
                }

                // Tạo danh sách SN để gửi đến API bên thứ ba
                var serialNumbers = string.Join(",", scrapRecords.Select(s => s.SN));
                Console.WriteLine($"Sending t_serial_numbers to external API: {serialNumbers}");

                // Gọi API bên thứ ba
                var externalRequest = new
                {
                    type = "task_system",
                    t_serial_numbers = serialNumbers
                };

                HttpResponseMessage externalResponse;
                List<ExternalApiResponse> externalData = null;

                try
                {
                    externalResponse = await _httpClient.PostAsJsonAsync("api/query", externalRequest);
                    externalResponse.EnsureSuccessStatusCode();
                    var responseContent = await externalResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"External API response: {responseContent}");
                    externalData = JsonSerializer.Deserialize<List<ExternalApiResponse>>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true // Bỏ qua phân biệt hoa thường
                    });
                }
                catch (HttpRequestException ex)
                {
                    // Log chi tiết lỗi khi gọi API bên thứ ba
                    return StatusCode(500, new { message = "Không thể kết nối đến API bên thứ ba.", error = ex.Message, innerException = ex.InnerException?.Message });
                }

                // Kết hợp dữ liệu từ ScrapList và API bên thứ ba
                var unmatchedSNs = new List<string>();
                var result = scrapRecords.Select(scrap =>
                {
                    var normalizedScrapSN = scrap.SN?.Trim();
                    var externalInfo = externalData?.FirstOrDefault(e =>
                    {
                        var normalizedBoardSN = e.BoardSN?.Trim();
                        Console.WriteLine($"Comparing: scrap.SN='{normalizedScrapSN}', external.BoardSN='{normalizedBoardSN}'");
                        return normalizedBoardSN == normalizedScrapSN;
                    });
                    if (externalInfo == null)
                    {
                        Console.WriteLine($"No matching data found for SN: {normalizedScrapSN}");
                        unmatchedSNs.Add(normalizedScrapSN);
                    }
                    else
                    {
                        Console.WriteLine($"Matched data for SN: {normalizedScrapSN}, BoardSN: {externalInfo.BoardSN}");
                    }

                    // Thử parse SmtTime thành DateTime nếu cần
                    DateTime? smtTime = null;
                    if (!string.IsNullOrEmpty(externalInfo?.SmtTime))
                    {
                        if (DateTime.TryParse(externalInfo.SmtTime, out DateTime parsedDate))
                        {
                            smtTime = parsedDate;
                        }
                        else
                        {
                            Console.WriteLine($"Invalid SmtTime format for SN: {normalizedScrapSN}, SmtTime: {externalInfo.SmtTime}");
                        }
                    }

                    return new
                    {
                        Purpose = scrap.Purpose,
                        BoardSN = scrap.SN,
                        AfterBeforeKanban = externalInfo?.AfterBeforeKanban ?? scrap.KanBanStatus,
                        Category = scrap.Category,
                        Sloc = scrap.Sloc,
                        TaskNumber = scrap.TaskNumber,
                        PONumber = scrap.PO,
                        CreateBy = scrap.CreatedBy,
                        CreateDate = scrap.CreateTime.ToString("yyyy-MM-dd"),
                        Cost = scrap.Cost,
                        Remark = scrap.Remark,
                        Item = externalInfo?.Item,
                        Project = externalInfo?.Project,
                        Opn = externalInfo?.Opn,
                        IcPn = externalInfo?.IcPn,
                        IcDetailPn = externalInfo?.IcDetailPn,
                        Qty = externalInfo?.Qty,
                        Cm = externalInfo?.Cm,
                        Plant = externalInfo?.Plant,
                        SmtTime = smtTime // Sử dụng giá trị đã parse (hoặc null nếu không parse được)
                    };
                }).ToList();

                // Thông báo nếu không tìm thấy dữ liệu từ API bên thứ ba
                string message = null;
                if (unmatchedSNs.Any())
                {
                    message = $"Không tìm thấy dữ liệu từ API bên thứ ba cho các SN: {string.Join(", ", unmatchedSNs)}";
                }

                // Xử lý cập nhật ApplyTaskStatus và ApplyTime nếu SaveApplyStatus = "1"
                if (request.SaveApplyStatus == "1")
                {
                    var currentTime = DateTime.Now; // Lấy thời gian hiện tại
                    var recordsToUpdate = _sqlContext.ScrapLists
                        .Where(s => request.InternalTasks.Contains(s.InternalTask) && s.ApplyTaskStatus == 0);

                    foreach (var record in recordsToUpdate)
                    {
                        record.ApplyTaskStatus = 1;
                        record.ApplyTime = currentTime; // Cập nhật ApplyTime
                    }

                    await _sqlContext.SaveChangesAsync();
                }

                return Ok(new { message, data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tạo task.", error = ex.Message, innerException = ex.InnerException?.Message });
            }
        }

        // API: Tạo task dựa trên danh sách SNs
        [HttpPost("create-task-sn")]
        public async Task<IActionResult> CreateTaskBySN([FromBody] CreateTaskBySNRequest request)
        {
            try
            {
                // Kiểm tra dữ liệu đầu vào
                if (request == null || request.SNs == null || !request.SNs.Any())
                {
                    return BadRequest(new { message = "Danh sách SNs không được để trống." });
                }

                if (string.IsNullOrEmpty(request.SaveApplyStatus) || (request.SaveApplyStatus != "0" && request.SaveApplyStatus != "1"))
                {
                    return BadRequest(new { message = "SaveApplyStatus phải là '0' hoặc '1'." });
                }

                // Kiểm tra xem tất cả SNs có tồn tại trong bảng ScrapList không
                var existingSNs = await _sqlContext.ScrapLists
                    .Where(s => request.SNs.Contains(s.SN))
                    .ToListAsync();

                var nonExistingSNs = request.SNs.Except(existingSNs.Select(s => s.SN)).ToList();
                if (nonExistingSNs.Any())
                {
                    return BadRequest(new { message = $"Các SN sau không tồn tại trong bảng ScrapList: {string.Join(", ", nonExistingSNs)}" });
                }

                // Kiểm tra trạng thái ApplyTaskStatus của các SN
                var rejectedSNs = new List<string>();
                var validSNs = new List<ScrapList>();

                foreach (var sn in existingSNs)
                {
                    if (sn.ApplyTaskStatus != 0 && sn.ApplyTaskStatus != 3)
                    {
                        string reason = sn.ApplyTaskStatus switch
                        {
                            1 => "đã có task",
                            2 => "đang chờ SPE approve scrap",
                            _ => $"trạng thái không hợp lệ ({sn.ApplyTaskStatus})"
                        };
                        rejectedSNs.Add($"{sn.SN} ({reason})");
                    }
                    else
                    {
                        validSNs.Add(sn); // SN hợp lệ để xử lý
                    }
                }

                if (rejectedSNs.Any())
                {
                    return BadRequest(new { message = $"Các SN sau không hợp lệ để tạo task: {string.Join(", ", rejectedSNs)}" });
                }

                if (!validSNs.Any())
                {
                    return BadRequest(new { message = "Không có SN nào hợp lệ để tạo task (yêu cầu ApplyTaskStatus = 0 hoặc 3)." });
                }

                // Tạo danh sách SN để gửi đến API bên thứ ba
                var serialNumbers = string.Join(",", validSNs.Select(s => s.SN));
                Console.WriteLine($"Sending t_serial_numbers to external API: {serialNumbers}");

                // Gọi API bên thứ ba
                var externalRequest = new
                {
                    type = "task_system",
                    t_serial_numbers = serialNumbers
                };

                HttpResponseMessage externalResponse;
                List<ExternalApiResponse> externalData = null;

                try
                {
                    externalResponse = await _httpClient.PostAsJsonAsync("api/query", externalRequest);
                    externalResponse.EnsureSuccessStatusCode();
                    var responseContent = await externalResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"External API response: {responseContent}");
                    externalData = JsonSerializer.Deserialize<List<ExternalApiResponse>>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true // Bỏ qua phân biệt hoa thường
                    });
                }
                catch (HttpRequestException ex)
                {
                    // Log chi tiết lỗi khi gọi API bên thứ ba
                    return StatusCode(500, new { message = "Không thể kết nối đến API bên thứ ba.", error = ex.Message, innerException = ex.InnerException?.Message });
                }

                // Kết hợp dữ liệu từ ScrapList và API bên thứ ba
                var unmatchedSNs = new List<string>();
                var result = validSNs.Select(scrap =>
                {
                    var normalizedScrapSN = scrap.SN?.Trim();
                    var externalInfo = externalData?.FirstOrDefault(e =>
                    {
                        var normalizedBoardSN = e.BoardSN?.Trim();
                        Console.WriteLine($"Comparing: scrap.SN='{normalizedScrapSN}', external.BoardSN='{normalizedBoardSN}'");
                        return normalizedBoardSN == normalizedScrapSN;
                    });
                    if (externalInfo == null)
                    {
                        Console.WriteLine($"No matching data found for SN: {normalizedScrapSN}");
                        unmatchedSNs.Add(normalizedScrapSN);
                    }
                    else
                    {
                        Console.WriteLine($"Matched data for SN: {normalizedScrapSN}, BoardSN: {externalInfo.BoardSN}");
                    }

                    // Thử parse SmtTime thành DateTime nếu cần
                    DateTime? smtTime = null;
                    if (!string.IsNullOrEmpty(externalInfo?.SmtTime))
                    {
                        if (DateTime.TryParse(externalInfo.SmtTime, out DateTime parsedDate))
                        {
                            smtTime = parsedDate;
                        }
                        else
                        {
                            Console.WriteLine($"Invalid SmtTime format for SN: {normalizedScrapSN}, SmtTime: {externalInfo.SmtTime}");
                        }
                    }

                    return new
                    {
                        Purpose = scrap.Purpose,
                        BoardSN = scrap.SN,
                        AfterBeforeKanban = externalInfo?.AfterBeforeKanban ?? scrap.KanBanStatus,
                        Category = scrap.Category,
                        Sloc = scrap.Sloc,
                        TaskNumber = scrap.TaskNumber,
                        PONumber = scrap.PO,
                        CreateBy = scrap.CreatedBy,
                        CreateDate = scrap.CreateTime.ToString("yyyy-MM-dd"),
                        Cost = scrap.Cost,
                        Remark = scrap.Remark,
                        Item = externalInfo?.Item,
                        Project = externalInfo?.Project,
                        Opn = externalInfo?.Opn,
                        IcPn = externalInfo?.IcPn,
                        IcDetailPn = externalInfo?.IcDetailPn,
                        Qty = externalInfo?.Qty,
                        Cm = externalInfo?.Cm,
                        Plant = externalInfo?.Plant,
                        SmtTime = smtTime // Sử dụng giá trị đã parse (hoặc null nếu không parse được)
                    };
                }).ToList();

                // Thông báo nếu không tìm thấy dữ liệu từ API bên thứ ba
                string message = null;
                if (unmatchedSNs.Any())
                {
                    message = $"Không tìm thấy dữ liệu từ API bên thứ ba cho các SN: {string.Join(", ", unmatchedSNs)}";
                }

                // Xử lý cập nhật ApplyTaskStatus và ApplyTime nếu SaveApplyStatus = "1"
                if (request.SaveApplyStatus == "1")
                {
                    var currentTime = DateTime.Now; // Lấy thời gian hiện tại
                    var recordsToUpdate = _sqlContext.ScrapLists
                        .Where(s => request.SNs.Contains(s.SN) && s.ApplyTaskStatus == 0);

                    foreach (var record in recordsToUpdate)
                    {
                        record.ApplyTaskStatus = 1;
                        record.ApplyTime = currentTime; // Cập nhật ApplyTime
                    }

                    await _sqlContext.SaveChangesAsync();
                }

                return Ok(new { message, data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tạo task.", error = ex.Message, innerException = ex.InnerException?.Message });
            }
        }

        // API: Cập nhật TaskNumber và PO cho danh sách SN
        [HttpPost("update-task-po")]
        public async Task<IActionResult> UpdateTaskPO([FromBody] UpdateTaskPORequest request)
        {
            try
            {
                // Kiểm tra dữ liệu đầu vào
                if (request == null)
                {
                    return BadRequest(new { message = "Yêu cầu không hợp lệ. Vui lòng kiểm tra dữ liệu đầu vào." });
                }

                // Kiểm tra và xử lý SnList
                var snList = request.SnList ?? new List<string>();
                if (!snList.Any())
                {
                    return BadRequest(new { message = "Danh sách SN không được để trống." });
                }

                if (string.IsNullOrEmpty(request.Task) || string.IsNullOrEmpty(request.PO))
                {
                    return BadRequest(new { message = "Task và PO không được để trống." });
                }

                // Kiểm tra độ dài các trường
                if (snList.Any(sn => sn?.Length > 50))
                {
                    return BadRequest(new { message = "SN không được dài quá 50 ký tự." });
                }

                if (request.Task.Length > 50 || request.PO.Length > 50)
                {
                    return BadRequest(new { message = "Task và PO không được dài quá 50 ký tự." });
                }

                // Kiểm tra xem tất cả SN có tồn tại trong bảng ScrapList không
                var existingSNs = await _sqlContext.ScrapLists
                    .Where(s => snList.Contains(s.SN))
                    .Select(s => s.SN)
                    .ToListAsync();

                var nonExistingSNs = snList.Except(existingSNs).ToList();
                if (nonExistingSNs.Any())
                {
                    return BadRequest(new { message = $"Các SN sau không tồn tại trong bảng ScrapList: {string.Join(", ", nonExistingSNs)}" });
                }

                // Kiểm tra xem các SN đã có TaskNumber hoặc PO chưa
                var recordsToUpdate = await _sqlContext.ScrapLists
                    .Where(s => snList.Contains(s.SN))
                    .ToListAsync();

                var rejectedSNs = new List<string>();
                foreach (var record in recordsToUpdate)
                {
                    if (record.TaskNumber != null || record.PO != null)
                    {
                        rejectedSNs.Add(record.SN);
                    }
                }

                if (rejectedSNs.Any())
                {
                    return BadRequest(new { message = $"Các SN sau đã có TaskNumber hoặc PO và không thể cập nhật: {string.Join(", ", rejectedSNs)}" });
                }

                // Cập nhật TaskNumber và PO cho các SN
                foreach (var record in recordsToUpdate)
                {
                    record.TaskNumber = request.Task;
                    record.PO = request.PO;
                    record.ApplyTaskStatus = 5;
                    record.ApplyTime = DateTime.Now;
                }

                await _sqlContext.SaveChangesAsync();

                return Ok(new { message = "Cập nhật TaskNumber và PO thành công cho các SN." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật dữ liệu.", error = ex.Message });
            }
        }

        // API mới: Cập nhật Cost cho danh sách Board SN
        [HttpPost("update-cost")]
        public async Task<IActionResult> UpdateCost([FromBody] UpdateCostRequest request)
        {
            try
            {
                // Kiểm tra dữ liệu đầu vào
                if (request == null)
                {
                    return BadRequest(new { message = "Yêu cầu không hợp lệ. Vui lòng kiểm tra dữ liệu đầu vào." });
                }

                // Kiểm tra và xử lý BoardSNs và Costs
                var boardSNs = request.BoardSNs ?? new List<string>();
                var costs = request.Costs ?? new List<double>();

                if (!boardSNs.Any() || !costs.Any())
                {
                    return BadRequest(new { message = "Danh sách Board SN và Cost không được để trống." });
                }

                if (boardSNs.Count != costs.Count)
                {
                    return BadRequest(new { message = "Số lượng Board SN và Cost không khớp nhau." });
                }

                // Kiểm tra độ dài của Board SN
                if (boardSNs.Any(sn => sn?.Length > 50))
                {
                    return BadRequest(new { message = "Board SN không được dài quá 50 ký tự." });
                }

                // Kiểm tra giá trị Cost hợp lệ (ví dụ: không âm)
                if (costs.Any(cost => cost < 0))
                {
                    return BadRequest(new { message = "Cost không được là số âm." });
                }

                // Kiểm tra xem tất cả Board SN có tồn tại trong bảng ScrapList không
                var existingSNs = await _sqlContext.ScrapLists
                    .Where(s => boardSNs.Contains(s.SN))
                    .Select(s => s.SN)
                    .ToListAsync();

                var nonExistingSNs = boardSNs.Except(existingSNs).ToList();
                if (nonExistingSNs.Any())
                {
                    return BadRequest(new { message = $"Các Board SN sau không tồn tại trong bảng ScrapList: {string.Join(", ", nonExistingSNs)}" });
                }

                // Kiểm tra xem các Board SN đã có Cost chưa
                var recordsToUpdate = await _sqlContext.ScrapLists
                    .Where(s => boardSNs.Contains(s.SN))
                    .ToListAsync();

                var rejectedSNs = new List<string>();
                foreach (var record in recordsToUpdate)
                {
                    // Kiểm tra nếu Cost không rỗng và không phải là chuỗi rỗng
                    if (!string.IsNullOrEmpty(record.Cost) && record.Cost != "")
                    {
                        rejectedSNs.Add(record.SN);
                    }
                }

                if (rejectedSNs.Any())
                {
                    return BadRequest(new { message = $"Các Board SN sau đã có Cost và không thể cập nhật: {string.Join(", ", rejectedSNs)}" });
                }

                // Cập nhật Cost cho các Board SN
                for (int i = 0; i < boardSNs.Count; i++)
                {
                    var record = recordsToUpdate.FirstOrDefault(r => r.SN == boardSNs[i]);
                    if (record != null)
                    {
                        record.Cost = costs[i].ToString(); // Chuyển Cost thành chuỗi để lưu vào cột Cost
                    }
                }

                await _sqlContext.SaveChangesAsync();

                return Ok(new { message = "Cập nhật Cost thành công cho các Board SN." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật Cost.", error = ex.Message });
            }
        }

        // API: Lấy dữ liệu từ ScrapList với ApplyTaskStatus = 1 (lịch sử áp dụng)
        [HttpGet("get-history-apply")]
        public async Task<IActionResult> GetHistoryApply()
        {
            try
            {
                // Lấy dữ liệu từ bảng ScrapList với ApplyTaskStatus = 1
                var scrapData = await _sqlContext.ScrapLists
                    .Where(s => s.ApplyTaskStatus == 1) // Lọc theo ApplyTaskStatus = 1
                    .GroupBy(s => s.InternalTask) // Nhóm theo InternalTask
                    .Select(g => new
                    {
                        InternalTask = g.Key,
                        Description = g.First().Desc,
                        ApproveScrapPerson = g.First().ApproveScrapperson,
                        KanBanStatus = g.First().KanBanStatus,
                        Category = g.First().Category,
                        Remark = g.First().Remark,
                        CreateTime = g.First().CreateTime.ToString("yyyy-MM-dd"),
                        CreateBy = g.First().CreatedBy,
                        ApplyTime = g.First().ApplyTime.HasValue ? g.First().ApplyTime.Value.ToString("yyyy-MM-dd") : "N/A",
                        ApplyTaskStatus = g.First().ApplyTaskStatus,
                        TotalQty = g.Count()
                    })
                    .ToListAsync();

                if (!scrapData.Any())
                {
                    return NotFound(new { message = "Không tìm thấy dữ liệu với ApplyTaskStatus = 1." });
                }

                return Ok(scrapData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy dữ liệu lịch sử.", error = ex.Message });
            }
        }

        // API: Lấy dữ liệu FindBoardStatus từ ScrapList
        [HttpGet("get-find-board-status")]
        public async Task<IActionResult> GetFindBoardStatus()
        {
            try
            {
                // Lấy dữ liệu từ bảng ScrapList và nhóm theo InternalTask
                var scrapData = await _sqlContext.ScrapLists
                    .GroupBy(s => s.InternalTask) // Nhóm theo InternalTask
                    .Select(g => new
                    {
                        InternalTask = g.Key,
                        ApproveScrapPerson = g.First().ApproveScrapperson, // Lấy giá trị đầu tiên
                        Description = g.First().Desc, // Lấy giá trị đầu tiên
                        AfterBefore = g.First().KanBanStatus, // Lấy giá trị đầu tiên của KanBanStatus (After/Before)
                        CreateTime = g.First().CreateTime.ToString("yyyy-MM-dd"), // Chỉ lấy ngày tháng năm
                        TotalQty = g.Count(), // Tổng số lượng SN trong InternalTask
                        QtyScraped = g.Count(s => s.FindBoardStatus == "Đã chuyển kho phế"), // Số lượng SN có trạng thái "Đã báo phế"
                        QtyFindOk = g.Count(s => s.FindBoardStatus == "Đã tìm thấy"), // Số lượng SN có trạng thái "Đã tìm thấy"
                        QtyWaitFind = g.Count(s => s.FindBoardStatus == "Chưa tìm thấy") // Số lượng SN có trạng thái "Chưa tìm thấy"
                    })
                    .ToListAsync();

                if (!scrapData.Any())
                {
                    return NotFound(new { message = "Không tìm thấy dữ liệu trong bảng ScrapList." });
                }

                // Tính toán Status dựa trên TotalQty và QtyScraped
                var result = scrapData.Select(item => new
                {
                    item.InternalTask,
                    item.ApproveScrapPerson,
                    item.Description,
                    AfterBefore = item.AfterBefore, // After/Before Kanban
                    item.CreateTime,
                    item.TotalQty,
                    item.QtyScraped,
                    item.QtyFindOk,
                    item.QtyWaitFind,
                    Status = item.QtyScraped == item.TotalQty ? "close" : "on-going" // Nếu QtyScraped = TotalQty thì "close", ngược lại "on-going"
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy dữ liệu FindBoardStatus.", error = ex.Message });
            }
        }



        // API: Lấy chi tiết dữ liệu từ ScrapList dựa trên InternalTasks hoặc SNs hoặc TaskNumber
        [HttpPost("detail-task-status")]
        public async Task<IActionResult> DetailTaskStatus([FromBody] DetailTaskStatusRequest request)
        {
            try
            {
                // Kiểm tra dữ liệu đầu vào
                if (request == null)
                {
                    return BadRequest(new { message = "Yêu cầu không hợp lệ. Vui lòng kiểm tra dữ liệu đầu vào." });
                }

                // Kiểm tra xem cả ba danh sách có trống không
                bool internalTasksEmpty = request.InternalTasks == null || !request.InternalTasks.Any();
                bool sNsEmpty = request.SNs == null || !request.SNs.Any();
                bool taskNumberEmpty = request.TaskNumber == null || !request.TaskNumber.Any();

                if (internalTasksEmpty && sNsEmpty && taskNumberEmpty)
                {
                    return BadRequest(new { message = "Cần cung cấp ít nhất một InternalTask hoặc một TaskNumber hoặc một SN." });
                }

                // Lấy dữ liệu từ bảng ScrapList
                IQueryable<ScrapList> query = _sqlContext.ScrapLists;

                // Nếu InternalTasks không trống, lọc theo InternalTasks
                if (!internalTasksEmpty)
                {
                    query = query.Where(s => request.InternalTasks.Contains(s.InternalTask));
                }

                // Nếu SNs không trống, lọc theo SNs
                if (!sNsEmpty)
                {
                    query = query.Where(s => request.SNs.Contains(s.SN));
                }

                // Nếu TaskNumber không trống, lọc theo TaskNumber
                if (!taskNumberEmpty)
                {
                    query = query.Where(s => request.TaskNumber.Contains(s.TaskNumber));
                }

                var scrapData = await query.ToListAsync();

                if (!scrapData.Any())
                {
                    return NotFound(new { message = "Không tìm thấy dữ liệu cho các InternalTasks hoặc SNs hoặc TaskNumber được cung cấp." });
                }

                // Chuyển dữ liệu thành định dạng trả về
                var result = scrapData.Select(s => new
                {
                    SN = s.SN,
                    InternalTask = s.InternalTask,
                    Description = s.Desc,
                    ApproveScrapPerson = s.ApproveScrapperson,
                    KanBanStatus = s.KanBanStatus,
                    Sloc = s.Sloc,
                    TaskNumber = s.TaskNumber,
                    PO = s.PO,
                    Cost = s.Cost,
                    Remark = s.Remark,
                    CreatedBy = s.CreatedBy,
                    CreateTime = s.CreateTime.ToString("yyyy-MM-dd"),
                    ApplyTime = s.ApplyTime.HasValue ? s.ApplyTime.Value.ToString("yyyy-MM-dd") : "N/A",
                    ApplyTaskStatus = s.ApplyTaskStatus,
                    FindBoardStatus = s.FindBoardStatus,
                    Purpose = s.Purpose,
                    Category = s.Category
                }).ToList();

                return Ok(new { data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy dữ liệu chi tiết.", error = ex.Message });
            }
        }

        // Class để nhận dữ liệu đầu vào cho API detail-task-status
        public class DetailTaskStatusRequest
        {
            public List<string> InternalTasks { get; set; } = new List<string>();
            public List<string> SNs { get; set; } = new List<string>();
            public List<string> TaskNumber { get; set; } = new List<string>();
        }


        // API: Cập nhật trạng thái FindBoardStatus trong bảng ScrapList
        [HttpPost("update-status-find-board")]
        public async Task<IActionResult> UpdateStatusFindBoard([FromBody] UpdateStatusFindBoardRequest request)
        {
            try
            {
                // Kiểm tra dữ liệu đầu vào
                if (request == null || request.SNs == null || !request.SNs.Any())
                {
                    return BadRequest(new { message = "Danh sách Serial Numbers (SNs) không được để trống." });
                }

                if (string.IsNullOrEmpty(request.Status))
                {
                    return BadRequest(new { message = "Trạng thái (Status) không được để trống." });
                }

                // Xác định giá trị FindBoardStatus dựa trên status đầu vào
                string findBoardStatus;
                switch (request.Status)
                {
                    case "1":
                        findBoardStatus = "Đã tìm thấy";
                        break;
                    case "2":
                        findBoardStatus = "Đã chuyển kho phế";
                        break;
                    default:
                        return BadRequest(new { message = "Trạng thái không hợp lệ. Chỉ chấp nhận giá trị '1' (Đã tìm thấy) hoặc '2' (Đã chuyển kho phế)." });
                }

                // Tìm các bản ghi trong ScrapList có SN khớp với danh sách SNs
                var scrapRecords = await _sqlContext.ScrapLists
                    .Where(s => request.SNs.Contains(s.SN))
                    .ToListAsync();

                if (!scrapRecords.Any())
                {
                    return NotFound(new { message = "Không tìm thấy bản ghi nào trong ScrapList khớp với danh sách SNs được cung cấp." });
                }

                // Cập nhật cột FindBoardStatus cho các bản ghi tìm thấy
                foreach (var record in scrapRecords)
                {
                    record.FindBoardStatus = findBoardStatus;
                }

                // Lưu thay đổi vào cơ sở dữ liệu
                await _sqlContext.SaveChangesAsync();

                return Ok(new { message = $"Cập nhật trạng thái FindBoardStatus thành công cho {scrapRecords.Count} bản ghi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật trạng thái FindBoardStatus.", error = ex.Message });
            }
        }

        // Class để nhận dữ liệu đầu vào cho API update-status-find-board
        public class UpdateStatusFindBoardRequest
        {
            public List<string> SNs { get; set; } = new List<string>();
            public string Status { get; set; } = string.Empty;
        }

        // API INPUT-SN-WAIT-SPE-APPROVE
        [HttpPost("input-sn-wait-spe-approve")]
        public async Task<IActionResult> InputSNWaitSpeApprove([FromBody] InputSNWaitSpeApproveRequest request)
        {
            try
            {
                // Kiểm tra dữ liệu đầu vào
                if (request == null || request.SNs == null || !request.SNs.Any())
                {
                    return BadRequest(new { message = "Danh sách SN không được để trống." });
                }

                if (string.IsNullOrEmpty(request.CreatedBy))
                {
                    return BadRequest(new { message = "CreatedBy không được để trống." });
                }

                if (string.IsNullOrEmpty(request.Remark))
                {
                    return BadRequest(new { message = "Remark không được để trống." });
                }

                if (string.IsNullOrEmpty(request.Approve))
                {
                    return BadRequest(new { message = "Approve không được để trống." });
                }

                // Kiểm tra độ dài các trường
                if (request.SNs.Any(sn => sn.Length > 50))
                {
                    return BadRequest(new { message = "SN không được dài quá 50 ký tự." });
                }

                if (request.CreatedBy.Length > 50 || (request.Description != null && request.Description.Length > 50))
                {
                    return BadRequest(new { message = "CreatedBy và Description không được dài quá 50 ký tự." });
                }

                

                // Kiểm tra SN trong bảng R117
                string connectionString = "User Id=TE;Password=B05te;Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=10.220.130.220)(PORT=1521)))(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME=vnsfc)))";

                using (var connection = new OracleConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Chuẩn bị câu lệnh SQL
                    string snList = string.Join(",", request.SNs.Select(sn => $"'{sn}'"));
                    string sqlQuery = $@"
                        SELECT SERIAL_NUMBER, MO_NUMBER 
                        FROM SFISM4.R117 
                        WHERE SERIAL_NUMBER IN ({snList}) 
                        AND GROUP_NAME = 'SMTLOADING' 
                        AND MO_NUMBER LIKE '5%'";


                    // Chỉ kiểm tra điều kiện MO_NUMBER nếu Approve = 2 (những bản cần xin báo phế)
                    if (request.Approve == "2")
                    {
                        
                        using (var command = new OracleCommand(sqlQuery, connection))
                        {
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                var invalidSNs = new List<string>();
                                while (await reader.ReadAsync())
                                {
                                    string serialNumber = reader.GetString(0);
                                    string moNumber = reader.GetString(1);
                                    invalidSNs.Add($"{serialNumber} (MO: {moNumber})");
                                }

                                if (invalidSNs.Any())
                                {
                                    return BadRequest(new { message = $"Các SN sau có MO_NUMBER bắt đầu bằng 5xxxx và không thể xử lý: {string.Join(", ", invalidSNs)}" });
                                }
                            }
                        }
                    }

                    // kiểm tra điều kiện Bonepile
                    if (request.Remark == "BP-10" || request.Remark == "BP-20")
                    {
                        string sqlQueryBonepile = $@"
                            SELECT SERIAL_NUMBER 
                            FROM sfism4.nvidia_bonpile_sn_log 
                            WHERE SERIAL_NUMBER IN ({snList})";

                        using (var command = new OracleCommand(sqlQueryBonepile, connection))
                        {
                            var foundSNs = new List<string>();
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    string serialNumber = reader.GetString(0);
                                    foundSNs.Add(serialNumber);
                                }
                            }

                            if (request.Remark == "BP-10")
                            {
                                // BP-10: Pass nếu không có SN nào trong bảng, reject nếu có bất kỳ SN nào
                                if (foundSNs.Any())
                                {
                                    return BadRequest(new { message = $"Các SN sau tồn tại trong bảng Bonepile 2.0: {string.Join(", ", foundSNs)}" });
                                }
                            }
                            else if (request.Remark == "BP-20")
                            {
                                // BP-20: Pass nếu tất cả SN đều có trong bảng, reject nếu thiếu bất kỳ SN nào
                                var missingSNs = request.SNs.Except(foundSNs).ToList();
                                if (missingSNs.Any())
                                {
                                    return BadRequest(new { message = $"Các SN sau không tồn tại trong bảng Bonepile 2.0: {string.Join(", ", missingSNs)}" });
                                }
                            }
                        }
                    }

                }



                // Kiểm tra trùng lặp SN trong bảng ScrapList
                var existingSNs = await _sqlContext.ScrapLists
                    .Where(s => request.SNs.Contains(s.SN))
                    .ToListAsync();

                var rejectedSNs = new List<string>();
                var updateSNs = new List<ScrapList>(); // Danh sách SN có ApplyTaskStatus = 3 để cập nhật
                var insertSNs = request.SNs.ToList(); // Danh sách SN để insert

                foreach (var sn in existingSNs)
                {
                    if (sn.ApplyTaskStatus == 0)
                    {
                        rejectedSNs.Add($"{sn.SN} (SN SPE đã approved phế, đang chờ xin Task/PO)");
                    }
                    else if (sn.ApplyTaskStatus == 1)
                    {
                        rejectedSNs.Add($"{sn.SN} (SN đã gửi NV xin Task/PO)");
                    }
                    else if (sn.ApplyTaskStatus == 2)
                    {
                        rejectedSNs.Add($"{sn.SN} (SN đang chờ SPE approve scrap)");
                    }
                    else if (sn.ApplyTaskStatus == 4)
                    {
                        rejectedSNs.Add($"{sn.SN} (SN đang chờ SPE approve BGA)");
                    }
                    else if (sn.ApplyTaskStatus == 5)
                    {
                        rejectedSNs.Add($"{sn.SN} (Đã có task, chờ chuyển MRB)");
                    }
                    else if (sn.ApplyTaskStatus == 6)
                    {
                        rejectedSNs.Add($"{sn.SN} (Đã chuyển kho phế, chờ MRB xác nhận)");
                    }
                    else if (sn.ApplyTaskStatus == 7)
                    {
                        rejectedSNs.Add($"{sn.SN} (Đã chuyển kho phế thành công)");
                    }
                    else if (sn.ApplyTaskStatus == 3)
                    {
                        updateSNs.Add(sn); // SN hợp lệ để cập nhật
                        insertSNs.Remove(sn.SN); // Loại bỏ SN khỏi danh sách insert
                    }
                    else
                    {
                        rejectedSNs.Add($"{sn.SN} (Trạng thái không xác định)");
                    }
                }

                if (rejectedSNs.Any())
                {
                    return BadRequest(new { message = $"Các SN sau đã có trong scrap list: {string.Join(", ", rejectedSNs)}" });
                }

                // Tạo biến phụ cho Approve chuyen xang dang int
                int ApproveTag = 0;

                if (request.Approve == "2")
                {
                    ApproveTag = 2;
                }
                else if (request.Approve == "4")
                {
                    ApproveTag = 4;
                }

                // Tạo danh sách ScrapList để lưu vào bảng (cho các SN mới)
                var scrapListEntries = new List<ScrapList>();
                foreach (var sn in insertSNs)
                {
                    var scrapEntry = new ScrapList
                    {
                        SN = sn,
                        KanBanStatus = "N/A",
                        Sloc = "N/A",
                        TaskNumber = null,
                        PO = null,
                        Cost = "N/A",
                        Remark = request.Remark,
                        CreatedBy = request.CreatedBy,
                        Desc = request.Description ?? "N/A",
                        CreateTime = DateTime.Now,
                        ApplyTime = null,
                        ApproveScrapperson = "N/A",
                        ApplyTaskStatus = ApproveTag,
                        FindBoardStatus = "N/A",
                        InternalTask = "N/A",
                        Purpose = "N/A",
                        Category = "N/A"
                    };

                    scrapListEntries.Add(scrapEntry);
                }

                // Cập nhật các bản ghi có ApplyTaskStatus = 3
                foreach (var sn in updateSNs)
                {
                    sn.KanBanStatus = "N/A";
                    sn.Sloc = "N/A";
                    sn.TaskNumber = null;
                    sn.PO = null;
                    sn.Cost = "N/A";
                    sn.Remark = request.Remark;
                    sn.CreatedBy = request.CreatedBy;
                    sn.Desc = request.Description ?? "N/A";
                    sn.CreateTime = DateTime.Now;
                    sn.ApplyTime = null;
                    sn.ApproveScrapperson = "N/A";
                    sn.ApplyTaskStatus = ApproveTag;
                    sn.FindBoardStatus = "N/A";
                    sn.InternalTask = "N/A";
                    sn.Purpose = "N/A";
                    sn.Category = "N/A";
                }

                // Lưu vào bảng ScrapList (thêm mới và cập nhật)
                if (scrapListEntries.Any())
                {
                    _sqlContext.ScrapLists.AddRange(scrapListEntries);
                }
                if (updateSNs.Any())
                {
                    _sqlContext.ScrapLists.UpdateRange(updateSNs);
                }
                await _sqlContext.SaveChangesAsync();

                string message = "Lưu danh sách SN thành công.";
                if (updateSNs.Any())
                {
                    message += $" Đã cập nhật {updateSNs.Count} SN có trạng thái 'SN SPE đã approve thay BGA'.";
                }

                return Ok(new { message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lưu dữ liệu.", error = ex.Message });
            }
        }


        // API: Lấy dữ liệu từ ScrapList với ApplyTaskStatus = 2 & 4
        [HttpGet("get-scrap-status-two-and-four")]
        public async Task<IActionResult> GetScrapStatusTwo()
        {
            try
            {
                // Lấy dữ liệu từ bảng ScrapList với ApplyTaskStatus = 2 & 4
                var scrapData = await _sqlContext.ScrapLists
                    .Where(s => s.ApplyTaskStatus == 2 || s.ApplyTaskStatus == 4) // Lọc theo ApplyTaskStatus = 2 & 4
                    .Select(s => new
                    {
                        SN = s.SN,
                        Description = s.Desc,
                        CreateTime = s.CreateTime.ToString("yyyy-MM-dd"),
                        ApplyTaskStatus = s.ApplyTaskStatus,
                        Remark = s.Remark,
                        CreateBy = s.CreatedBy
                    })
                    .ToListAsync();

                if (!scrapData.Any())
                {
                    return NotFound(new { message = "Không tìm thấy dữ liệu với ApplyTaskStatus = 2." });
                }

                return Ok(scrapData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy dữ liệu.", error = ex.Message });
            }
        }

        // API: Lấy danh sách TaskNumber và tổng số lượng SN theo ApplyTaskStatus
        [HttpGet("get-task-by-status")]
        public async Task<IActionResult> GetTaskByStatus([FromQuery] int status)
        {
            try
            {
                var result = await _sqlContext.ScrapLists
                    .Where(s => s.ApplyTaskStatus == status && !string.IsNullOrEmpty(s.TaskNumber))
                    .GroupBy(s => s.TaskNumber)
                    .Select(g => new
                    {
                        TaskNumber = g.Key,
                        ApplyTime = g.Min(s => s.ApplyTime) != null ? g.Min(s => s.ApplyTime).Value.ToString("yyyy-MM-dd") : "N/A",
                        TotalQty = g.Count()
                    })
                    .ToListAsync();

                if (!result.Any())
                {
                    return NotFound(new { message = $"Không tìm thấy TaskNumber với ApplyTaskStatus = {status}." });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy dữ liệu.", error = ex.Message });
            }
        }

        // API: Xác nhận chuyển MRB
        [HttpPost("confirm-move-mrb")]
        public async Task<IActionResult> ConfirmMoveMRB([FromBody] ConfirmMoveMRBRequest request)
        {
            try
            {
                // Kiểm tra dữ liệu đầu vào
                if (request == null)
                {
                    return BadRequest(new { message = "Yêu cầu không hợp lệ. Vui lòng kiểm tra dữ liệu đầu vào." });
                }

                var snList = request.SNs ?? new List<string>();
                var taskNumberList = request.TaskNumbers ?? new List<string>();

                if (!snList.Any() || !taskNumberList.Any())
                {
                    return BadRequest(new { message = "Danh sách SNs và TaskNumbers không được để trống." });
                }

                // Kiểm tra độ dài của SN và TaskNumber
                if (snList.Any(sn => sn?.Length > 50) || taskNumberList.Any(task => task?.Length > 50))
                {
                    return BadRequest(new { message = "SN hoặc TaskNumber không được dài quá 50 ký tự." });
                }

                // Lấy tất cả các bản ghi từ ScrapList dựa trên TaskNumbers
                var scrapRecords = await _sqlContext.ScrapLists
                    .Where(s => taskNumberList.Contains(s.TaskNumber))
                    .ToListAsync();

                if (!scrapRecords.Any())
                {
                    return NotFound(new { message = "Không tìm thấy dữ liệu trong ScrapList cho các TaskNumbers được cung cấp." });
                }

                // Lấy danh sách SN từ ScrapList dựa trên TaskNumbers
                var scrapSNs = scrapRecords.Select(s => s.SN).ToList();

                // So sánh SNs từ đầu vào với SNs từ ScrapList
                var unmatchedSNs = snList.Except(scrapSNs).ToList();
                var matchedSNs = snList.Intersect(scrapSNs).ToList();

                // Nếu có bất kỳ SN nào không khớp, không thực hiện cập nhật
                if (unmatchedSNs.Any())
                {
                    return BadRequest(new { message = $"Các SN sau không khớp với dữ liệu từ TaskNumbers: {string.Join(", ", unmatchedSNs)}. Không thực hiện cập nhật." });
                }

                // Nếu tất cả SN khớp, thực hiện cập nhật
                var updatedRecords = new List<ScrapList>();
                foreach (var sn in matchedSNs)
                {
                    var record = scrapRecords.FirstOrDefault(r => r.SN == sn);
                    if (record != null)
                    {
                        if (record.ApplyTaskStatus == 5)
                        {
                            record.ApplyTaskStatus = 6;
                            record.ApplyTime = DateTime.Now;
                            updatedRecords.Add(record);
                        }
                        else if (record.ApplyTaskStatus == 6)
                        {
                            record.ApplyTaskStatus = 7;
                            record.ApplyTime = DateTime.Now;
                            updatedRecords.Add(record);
                        }
                        // Nếu ApplyTaskStatus không phải 5 hoặc 6, giữ nguyên (không cập nhật)
                    }
                }

                if (!updatedRecords.Any())
                {
                    return Ok(new { message = "Không có bản ghi nào được cập nhật do trạng thái không hợp lệ." });
                }

                // Lưu thay đổi vào cơ sở dữ liệu
                await _sqlContext.SaveChangesAsync();

                // Chuẩn bị dữ liệu để gọi API bên thứ ba
                var snListString = string.Join(",", matchedSNs);
                var status = updatedRecords[0].ApplyTaskStatus.ToString(); // Lấy trạng thái cuối cùng sau khi cập nhật

                var externalRequest = new
                {
                    type = "update",
                    sn_list = snListString,
                    type_bp = (string)null,
                    status = status,
                    task = (string)null
                };

                // Gọi API bên thứ ba
                var externalResponse = await _httpClient.PostAsJsonAsync("https://sfc-portal.cns.myfiinet.com/SfcSmartRepair/api/repair_scrap", externalRequest);
                externalResponse.EnsureSuccessStatusCode();
                var externalResult = await externalResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"External API response: {externalResult}");

                string message = "Cập nhật trạng thái ApplyTaskStatus thành công.";
                return Ok(new { message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xác nhận chuyển MRB.", error = ex.Message });
            }
        }

        // Class để nhận dữ liệu đầu vào cho API confirm-move-mrb
        public class ConfirmMoveMRBRequest
        {
            public List<string> SNs { get; set; } = new List<string>();
            public List<string> TaskNumbers { get; set; } = new List<string>();
        }



    }

    // Class để nhận dữ liệu đầu vào cho API update-cost
    public class UpdateCostRequest
    {
        public List<string> BoardSNs { get; set; } = new List<string>();
        public List<double> Costs { get; set; } = new List<double>();
    }

    // Class để nhận dữ liệu đầu vào cho API update-task-po
    public class UpdateTaskPORequest
    {
        public List<string> SnList { get; set; } = new List<string>();
        public string Task { get; set; } = string.Empty;
        public string PO { get; set; } = string.Empty;
    }

    // Class để nhận dữ liệu đầu vào cho API create-task
    public class CreateTaskRequest
    {
        public List<string> InternalTasks { get; set; } = new List<string>();
        public string SaveApplyStatus { get; set; } = string.Empty;
    }

    // Class để nhận dữ liệu đầu vào cho API create-task-sn
    public class CreateTaskBySNRequest
    {
        public List<string> SNs { get; set; } = new List<string>();
        public string SaveApplyStatus { get; set; } = string.Empty;
    }

    // Class để nhận dữ liệu đầu vào cho API input-sn
    public class InputSNRequest
    {
        public List<string> SNs { get; set; } = new List<string>();
        public string CreatedBy { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ApproveScrapPerson { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
    }

    // Class để ánh xạ dữ liệu từ API bên thứ ba
    public class ExternalApiResponse
    {
        public double? Item { get; set; }
        public string Project { get; set; }
        public string Opn { get; set; }
        [JsonPropertyName("ic pn")]
        public string IcPn { get; set; }
        [JsonPropertyName("ic detail pn")]
        public string IcDetailPn { get; set; }
        [JsonPropertyName("boarch sn")]
        public string BoardSN { get; set; }
        [JsonPropertyName("smt time")]
        public string SmtTime { get; set; }
        public string Qty { get; set; }
        [JsonPropertyName("after/before kanban")]
        public string AfterBeforeKanban { get; set; }
        public string Cm { get; set; }
        public string Plant { get; set; }
    }

    // Class để nhận dữ liệu đầu vào cho API input-sn-wait-spe-approve
    public class InputSNWaitSpeApproveRequest
    {
        public List<string> SNs { get; set; } = new List<string>();
        public string Description { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
        public string Approve { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
    }

}