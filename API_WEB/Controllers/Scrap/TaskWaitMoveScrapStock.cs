using API_WEB.ModelsDB;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace API_WEB.Controllers.Scrap
{
    public class TaskWaitMoveScrapStock : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly HttpClient _httpClient;

        public TaskWaitMoveScrapStock(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory)
        {
            _scopeFactory = scopeFactory;

            // Cấu hình HttpClient với HttpClientHandler để bỏ qua chứng chỉ SSL (tương tự ScrapController)
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true // Bỏ qua kiểm tra chứng chỉ
            };
            _httpClient = new HttpClient(handler);
            _httpClient.BaseAddress = new Uri("https://10.220.130.217:443/SfcSmartRepair/");
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("🚀 TaskWaitMoveScrapStock STARTED...");
            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<CSDL_NE>();

                    var now = DateTime.Now;
                    var targetTime = new DateTime(now.Year, now.Month, now.Day, 7, 0, 0); // Chạy vào 7h sáng

                    Console.WriteLine($"Current time: {now:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"Target time: {targetTime:yyyy-MM-dd HH:mm:ss}");

                    if (now > targetTime)
                    {
                        targetTime = targetTime.AddDays(1);
                        Console.WriteLine($"Adjusted target time (added 1 day): {targetTime:yyyy-MM-dd HH:mm:ss}");
                    }

                    var delay = targetTime - now;
                    Console.WriteLine($"Delay: {delay.TotalSeconds} seconds");
                    await Task.Delay(delay, stoppingToken); // Chờ đến thời gian chạy

                    try
                    {
                        Console.WriteLine($"Service running at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                        // Lấy dữ liệu từ bảng ScrapList để kiểm tra InternalTask đã có TaskNumber
                        var internalTasksWithTaskNumber = await dbContext.ScrapLists
                            .Where(s => !string.IsNullOrEmpty(s.TaskNumber)) // Lọc các bản ghi đã có TaskNumber
                            .GroupBy(s => s.InternalTask) // Nhóm theo InternalTask
                            .Select(g => new
                            {
                                InternalTask = g.Key,
                                TaskNumber = g.First().TaskNumber, // Lấy TaskNumber đầu tiên (giả sử tất cả bản ghi trong cùng InternalTask có cùng TaskNumber)
                                Description = g.First().Desc,
                                ApproveScrapPerson = g.First().ApproveScrapperson,
                                CreateTime = g.First().CreateTime,
                                ApplyTaskStatus = g.First().ApplyTaskStatus,
                                TotalQty = g.Count()
                            })
                            .ToListAsync(stoppingToken);

                        Console.WriteLine($"Tổng số InternalTask đã có TaskNumber: {internalTasksWithTaskNumber.Count}");

                        if (!internalTasksWithTaskNumber.Any())
                        {
                            Console.WriteLine("Không có InternalTask nào đã có TaskNumber.");
                            continue; // Chuyển sang vòng lặp tiếp theo
                        }

                        // Gọi API để lấy danh sách stock_no
                        var stockData = await GetStockDataAsync();
                        if (stockData == null || !stockData.Any())
                        {
                            Console.WriteLine("Không lấy được dữ liệu từ API stock_and_ship_no.");
                            continue; // Chuyển sang vòng lặp tiếp theo
                        }

                        // Lấy danh sách stock_no từ dữ liệu API
                        var stockNos = stockData.Select(s => s.StockNo).ToList();
                        Console.WriteLine($"Danh sách stock_no từ API: {string.Join(", ", stockNos)}");

                        // Tìm các InternalTask có TaskNumber không nằm trong danh sách stock_no
                        var tasksNotInStock = internalTasksWithTaskNumber
                            .Where(t => !stockNos.Contains(t.TaskNumber))
                            .ToList();

                        Console.WriteLine($"Tổng số InternalTask chưa được chuyển vào kho phế: {tasksNotInStock.Count}");

                        if (tasksNotInStock.Any())
                        {
                            // Tạo nội dung email
                            string subject = "Cảnh báo: InternalTask đã có TaskNumber nhưng chưa được chuyển vào kho phế";
                            string body = "<h2>Cảnh báo: Các InternalTask sau chưa được chuyển vào kho phế</h2>";
                            body += "<p>Danh sách các InternalTask đã có TaskNumber nhưng chưa được chuyển vào kho phế:</p>";
                            body += "<table border='1' style='border-collapse: collapse; width: 100%;'>";
                            body += "<tr style='background-color: #f2f2f2;'><th>InternalTask</th><th>TaskNumber</th><th>Description</th><th>Approve Scrap Person</th><th>Create Time</th><th>Apply Task Status</th><th>Total Q'ty</th></tr>";

                            foreach (var task in tasksNotInStock)
                            {
                                body += "<tr>";
                                body += $"<td>{task.InternalTask}</td>";
                                body += $"<td>{task.TaskNumber}</td>";
                                body += $"<td>{task.Description}</td>";
                                body += $"<td>{task.ApproveScrapPerson}</td>";
                                body += $"<td>{task.CreateTime:yyyy-MM-dd}</td>";
                                body += $"<td>{task.ApplyTaskStatus}</td>";
                                body += $"<td>{task.TotalQty}</td>";
                                body += "</tr>";
                            }

                            body += "</table>";
                            body += "<p>Vui lòng kiểm tra và chuyển các InternalTask trên vào kho phế.</p>";

                            // Danh sách email cố định (giữ nguyên như ScrapTaskNumberAlertService)
                            string toEmails = "jax.fw.ruan@mail.foxconn.com,mbd-vn-pe-nvidia@mail.foxconn.com,mark.ds.ruan@fii-foxconn.com,jue.tj.wu@mail.foxconn.com,neo.wn.huang@mail.foxconn.com,cpeii-vn-re@mail.foxconn.com,sunshine.qx.fan@mail.foxconn.com";

                            // Gửi email cảnh báo
                            Console.WriteLine("Sending email alert...");
                            await MailHelper.SendEmailAsync(toEmails, subject, body);
                        }
                        else
                        {
                            Console.WriteLine("Không có InternalTask nào đã có TaskNumber nhưng chưa được chuyển vào kho phế.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in TaskWaitMoveScrapStock: {ex.Message}");
                        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    }
                }
            }
        }

        private async Task<List<StockAndShipNoResponse>> GetStockDataAsync()
        {
            try
            {
                var requestBody = new { type = "stock_and_ship_no" };
                var response = await _httpClient.PostAsJsonAsync("api/query", requestBody);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"API response: {responseContent}");

                var stockData = JsonSerializer.Deserialize<List<StockAndShipNoResponse>>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true // Bỏ qua phân biệt hoa thường
                });

                return stockData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling stock_and_ship_no API: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return null;
            }
        }
    }

    // Class để ánh xạ dữ liệu từ API stock_and_ship_no
    public class StockAndShipNoResponse
    {
        public string StockNo { get; set; }
        public string ShipNo { get; set; }
    }
}