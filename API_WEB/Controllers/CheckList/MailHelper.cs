using System;
using System.Net.Mail;
using System.Net;
using System.Threading.Tasks;

public class MailHelper
{
    private static string smtpServer = "10.134.28.95"; // SMTP Server bạn cung cấp
    private static int smtpPort = 25; // Cổng mặc định (nếu khác thì sửa)
    private static string fromEmail = "mbd-vn-pe-nvidia@mail.foxconn.com"; // Email gửi đi

    public static async Task SendEmailAsync(string toEmails, string subject, string body, string attachmentPath = null)
    {
        try
        {
            Console.WriteLine($"Preparing to send email...");
            Console.WriteLine($"SMTP Server: {smtpServer}, Port: {smtpPort}");
            Console.WriteLine($"From: {fromEmail}");
            Console.WriteLine($"To: {toEmails}");
            Console.WriteLine($"Subject: {subject}");
            Console.WriteLine($"Body: {body}");
            if (attachmentPath != null)
            {
                Console.WriteLine($"Attachment: {attachmentPath}");
            }

            using (var client = new SmtpClient(smtpServer, smtpPort))
            {
                client.UseDefaultCredentials = true; // Nếu SMTP không yêu cầu username/password
                client.EnableSsl = false; // Đặt thành true nếu server cần SSL

                // Thêm timeout để tránh treo nếu server không phản hồi
                client.Timeout = 10000; // 10 giây

                MailMessage mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                // Thêm danh sách người nhận (các email cách nhau bởi dấu phẩy)
                foreach (var email in toEmails.Split(','))
                {
                    mailMessage.To.Add(email.Trim());
                }

                // Đính kèm file nếu có
                if (!string.IsNullOrEmpty(attachmentPath) && File.Exists(attachmentPath))
                {
                    mailMessage.Attachments.Add(new Attachment(attachmentPath));
                }

                Console.WriteLine("Sending email...");
                await client.SendMailAsync(mailMessage);
                Console.WriteLine("Email sent successfully.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error sending email: " + ex.Message);
            Console.WriteLine($"Lỗi khi gửi email đến {toEmails}: {ex.Message}");
            Console.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            throw;
        }
    }
}