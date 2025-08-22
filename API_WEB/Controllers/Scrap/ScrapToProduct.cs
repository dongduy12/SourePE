using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using API_WEB.ModelsDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace API_WEB.Controllers.Scrap
{
    public class ScrapToProduct : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly int _intervalInHours = 2; // Chạy mỗi 2 giờ

        public ScrapToProduct(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Tạo scope mới để sử dụng CSDL_NE
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<CSDL_NE>();

                        // Lấy tất cả SN từ bảng Product và ScrapList
                        var productSNs = await context.Products
                            .Where(p => string.IsNullOrEmpty(p.Scrap)) // Chỉ lấy các SN có cột Scrap trống
                            .Select(p => p.SerialNumber)
                            .ToListAsync(stoppingToken);

                        var scrapListSNs = await context.ScrapLists
                            .Select(s => s.SN)
                            .ToListAsync(stoppingToken);

                        // Tìm các SN chung giữa hai bảng
                        var commonSNs = productSNs.Intersect(scrapListSNs).ToList();

                        if (!commonSNs.Any())
                        {
                            Console.WriteLine("Không tìm thấy SN chung giữa Product và ScrapList.");
                            await Task.Delay(TimeSpan.FromHours(_intervalInHours), stoppingToken);
                            continue;
                        }

                        // Lấy thông tin từ ScrapList dựa trên các SN chung
                        var scrapRecords = await context.ScrapLists
                            .Where(s => commonSNs.Contains(s.SN))
                            .ToListAsync(stoppingToken);

                        foreach (var scrapRecord in scrapRecords)
                        {
                            var product = await context.Products
                                .FirstOrDefaultAsync(p => p.SerialNumber == scrapRecord.SN && string.IsNullOrEmpty(p.Scrap), stoppingToken);

                            if (product != null)
                            {
                                // Cập nhật cột Scrap dựa trên ApplyTaskStatus
                                switch (scrapRecord.ApplyTaskStatus)
                                {
                                    case 0:
                                    case 1:
                                        product.Scrap = "Đã được NV approved scrap";
                                        break;
                                    case 2:
                                        product.Scrap = "Đã gửi NV xin báo phế";
                                        break;
                                    case 3:
                                        product.Scrap = "Đã được NV approved BGA";
                                        break;
                                    default:
                                        product.Scrap = "Trạng thái không xác định";
                                        break;
                                }

                                // Đánh dấu bản ghi đã thay đổi
                                context.Entry(product).State = EntityState.Modified;
                            }
                        }

                        // Lưu thay đổi vào cơ sở dữ liệu
                        await context.SaveChangesAsync(stoppingToken);
                        Console.WriteLine($"Cập nhật cột Scrap thành công vào lúc: {DateTime.Now}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi khi cập nhật cột Scrap: {ex.Message}");
                }

                // Đợi đến lần chạy tiếp theo
                await Task.Delay(TimeSpan.FromHours(_intervalInHours), stoppingToken);
            }
        }
    }
}