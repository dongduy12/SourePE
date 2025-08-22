using API_WEB.ModelsOracle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace API_WEB.Controllers.SmartFA
{
    public class Alert : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        // Constructor nhận IServiceScopeFactory để tạo scope cho DbContext
        public Alert(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            // Đặt license context cho EPPlus (NonCommercial nếu không dùng thương mại)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        // Phương thức chính của BackgroundService
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("🚀 AlertService STARTED...");
            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<OracleDbContext>();

                    try
                    {
                        Console.WriteLine($"Service running at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                        // 1. Truy vấn dữ liệu từ bảng R_REPAIR_TASK_T
                        var overdueSNs = await GetOverdueSerialNumbersAsync(dbContext, stoppingToken);

                        // 2. Nếu không có SN nào vi phạm, ghi log và tiếp tục vòng lặp
                        if (!overdueSNs.Any())
                        {
                            Console.WriteLine("Không có SN nào vượt quá 1 giờ chưa được FA nhận.");
                        }
                        else
                        {
                            // 3. Tạo file Excel từ dữ liệu truy vấn
                            string excelFilePath = await GenerateExcelFileAsync(overdueSNs);

                            // 4. Gửi email cảnh báo kèm file Excel
                            await SendAlertEmailAsync(overdueSNs.Count, excelFilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in AlertService: {ex.Message}");
                        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    }

                    // Chờ 1 giờ trước khi chạy lại
                    Console.WriteLine("Waiting for 1 hour before the next check...");
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }
        }

        // Truy vấn các SN vượt quá 1 giờ chưa được FA nhận
        private async Task<List<R_REPAIR_TASK_T>> GetOverdueSerialNumbersAsync(OracleDbContext dbContext, CancellationToken stoppingToken)
        {
            // Thời gian hiện tại trừ 1 giờ
            var oneHourAgo = DateTime.Now.AddHours(-1);

            // Truy vấn dữ liệu từ bảng R_REPAIR_TASK_T
            var overdueSNs = await dbContext.OracleDataRepairTask
                .Where(r => r.DATA12 == "CHECK_IN"
                         && r.DATA11 == "FA"
                         && r.DATA13 == "WAITING_HAND_OVER"
                         && r.DATE3 != null
                         && r.DATE3 <= oneHourAgo)
                .Select(r => new R_REPAIR_TASK_T
                {
                    SERIAL_NUMBER = r.SERIAL_NUMBER,
                    MO_NUMBER = r.MO_NUMBER,
                    MODEL_NAME = r.MODEL_NAME,
                    TEST_CODE = r.TEST_CODE,
                    TEST_GROUP = r.TEST_GROUP,
                    DATA12 = r.DATA12,
                    DATA11 = r.DATA11,
                    DATA13 = r.DATA13,
                    DATE3 = r.DATE3
                })
                .ToListAsync(stoppingToken);

            return overdueSNs;
        }

        // Tạo file Excel từ danh sách SN
        private async Task<string> GenerateExcelFileAsync(List<R_REPAIR_TASK_T> overdueSNs)
        {
            // Đường dẫn lưu file Excel tạm thời
            string fileName = $"OverdueSNs_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            string filePath = Path.Combine(Path.GetTempPath(), fileName);

            // Sử dụng EPPlus để tạo file Excel
            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var worksheet = package.Workbook.Worksheets.Add("OverdueSNs");

                // Định nghĩa tiêu đề cột
                worksheet.Cells[1, 1].Value = "SERIAL_NUMBER";
                worksheet.Cells[1, 2].Value = "MO_NUMBER";
                worksheet.Cells[1, 3].Value = "MODEL_NAME";
                worksheet.Cells[1, 4].Value = "TEST_CODE";
                worksheet.Cells[1, 5].Value = "TEST_GROUP";
                worksheet.Cells[1, 6].Value = "DATA12";
                worksheet.Cells[1, 7].Value = "DATA11";
                worksheet.Cells[1, 8].Value = "DATA13";
                worksheet.Cells[1, 9].Value = "DATE3";

                // Định dạng tiêu đề
                using (var range = worksheet.Cells[1, 1, 1, 9])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }

                // Ghi dữ liệu vào file Excel
                for (int i = 0; i < overdueSNs.Count; i++)
                {
                    var sn = overdueSNs[i];
                    worksheet.Cells[i + 2, 1].Value = sn.SERIAL_NUMBER;
                    worksheet.Cells[i + 2, 2].Value = sn.MO_NUMBER;
                    worksheet.Cells[i + 2, 3].Value = sn.MODEL_NAME;
                    worksheet.Cells[i + 2, 4].Value = sn.TEST_CODE;
                    worksheet.Cells[i + 2, 5].Value = sn.TEST_GROUP;
                    worksheet.Cells[i + 2, 6].Value = sn.DATA12;
                    worksheet.Cells[i + 2, 7].Value = sn.DATA11;
                    worksheet.Cells[i + 2, 8].Value = sn.DATA13;
                    worksheet.Cells[i + 2, 9].Value = sn.DATE3?.ToString("yyyy-MM-dd HH:mm:ss");
                }

                // Tự động điều chỉnh kích thước cột
                worksheet.Cells.AutoFitColumns();

                // Lưu file Excel
                await package.SaveAsync();
            }

            return filePath;
        }

        // Gửi email cảnh báo kèm file Excel
        private async Task SendAlertEmailAsync(int snCount, string excelFilePath)
        {
            // Tiêu đề email
            string subject = "Cảnh báo: SN vượt quá 1 giờ chưa được FA nhận";

            // Nội dung email
            string body = $@"
                <h3>Cảnh báo hệ thống SmartFA</h3>
                <p>Phát hiện <strong>{snCount}</strong> Serial Number đã chuyển từ CHECK_IN sang FA nhưng vượt quá 1 giờ mà vẫn chưa được FA nhận.</p>
                <p>Vui lòng xem chi tiết trong file Excel đính kèm.</p>
                <p>Trân trọng,<br/>Hệ thống SmartFA</p>";

            // Danh sách email cố định
            string toEmails = "jax.fw.ruan@mail.foxconn.com,mark.ds.ruan@fii-foxconn.com,jue.tj.wu@mail.foxconn.com,neo.wn.huang@mail.foxconn.com,cpeii-vn-re@mail.foxconn.com";

            try
            {
                // Gửi email sử dụng MailHelper, kèm file Excel
                Console.WriteLine("Sending email alert...");
                await MailHelper.SendEmailAsync(toEmails, subject, body, excelFilePath);
                Console.WriteLine($"Email sent successfully to {toEmails}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi gửi email cảnh báo: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
            finally
            {
                // Xóa file Excel tạm sau khi gửi email
                if (File.Exists(excelFilePath))
                {
                    File.Delete(excelFilePath);
                }
            }
        }
    }
}