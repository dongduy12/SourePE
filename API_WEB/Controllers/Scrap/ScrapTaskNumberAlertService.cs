using API_WEB.ModelsDB;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class ScrapTaskNumberAlertService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScrapTaskNumberAlertService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("🚀ScrapTaskNumberAlertService STARTED...");
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<CSDL_NE>();

                var now = DateTime.Now;
                var targetTime = new DateTime(now.Year, now.Month, now.Day, 6, 0, 0); // Đặt thời gian chạy vào 6h sáng

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

                    // Lấy dữ liệu từ bảng ScrapList để kiểm tra InternalTask chưa có TaskNumber
                    var internalTasksWithoutTaskNumber = await dbContext.ScrapLists
                        .Where(s => s.TaskNumber == null || s.TaskNumber == "") // Lọc các bản ghi chưa có TaskNumber
                        .GroupBy(s => s.InternalTask) // Nhóm theo InternalTask
                        .Select(g => new
                        {
                            InternalTask = g.Key,
                            Description = g.First().Desc,
                            ApproveScrapPerson = g.First().ApproveScrapperson,
                            CreateTime = g.First().CreateTime,
                            ApplyTaskStatus = g.First().ApplyTaskStatus, // Thêm cột ApplyTaskStatus
                            TotalQty = g.Count()
                        })
                        .ToListAsync(stoppingToken);

                    Console.WriteLine($"Tổng số InternalTask chưa có TaskNumber: {internalTasksWithoutTaskNumber.Count}");

                    if (internalTasksWithoutTaskNumber.Any())
                    {
                        // Tạo nội dung email
                        string subject = "Cảnh báo: InternalTask chưa có TaskNumber trong ScrapList";
                        string body = "<h2>Cảnh báo: Các InternalTask sau chưa có TaskNumber</h2>";
                        body += "<p>Danh sách các InternalTask chưa được cập nhật TaskNumber trong bảng ScrapList:</p>";
                        body += "<table border='1' style='border-collapse: collapse; width: 100%;'>";
                        body += "<tr style='background-color: #f2f2f2;'><th>InternalTask</th><th>Description</th><th>Approve Scrap Person</th><th>Create Time</th><th>Apply Task Status</th><th>Total Q'ty</th></tr>";

                        foreach (var task in internalTasksWithoutTaskNumber)
                        {
                            body += "<tr>";
                            body += $"<td>{task.InternalTask}</td>";
                            body += $"<td>{task.Description}</td>";
                            body += $"<td>{task.ApproveScrapPerson}</td>";
                            body += $"<td>{task.CreateTime:yyyy-MM-dd}</td>";
                            body += $"<td>{task.ApplyTaskStatus}</td>"; // Thêm giá trị ApplyTaskStatus vào bảng
                            body += $"<td>{task.TotalQty}</td>";
                            body += "</tr>";
                        }

                        body += "</table>";
                        body += "<p>Vui lòng kiểm tra và cập nhật TaskNumber cho các InternalTask trên.</p>";

                        // Danh sách email cố định
                        string toEmails = "jax.fw.ruan@mail.foxconn.com,mbd-vn-pe-nvidia@mail.foxconn.com,mark.ds.ruan@fii-foxconn.com,jue.tj.wu@mail.foxconn.com,neo.wn.huang@mail.foxconn.com,cpeii-vn-re@mail.foxconn.com,sunshine.qx.fan@mail.foxconn.com";

                        // Gửi email cảnh báo
                        Console.WriteLine("Sending email alert...");
                        await MailHelper.SendEmailAsync(toEmails, subject, body);
                    }
                    else
                    {
                        Console.WriteLine("Không có InternalTask nào chưa có TaskNumber.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in ScrapTaskNumberAlertService: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                }
            }
        }
    }
}