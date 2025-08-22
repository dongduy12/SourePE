using API_WEB.ModelsDB;
using Microsoft.Extensions.Hosting;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public class DailyTaskService : BackgroundService
{
    private readonly HttpClient _httpClient;
    private readonly IServiceScopeFactory _scopeFactory;

    public DailyTaskService(IServiceScopeFactory scopeFactory)
    {
        _httpClient = new HttpClient();
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<CSDL_NE>();

                var now = DateTime.Now;
                var targetTime = new DateTime(now.Year, now.Month, now.Day, 6, 0, 0);

                if (now > targetTime)
                {
                    targetTime = targetTime.AddDays(1);
                }

                var delay = targetTime - now;
                await Task.Delay(delay, stoppingToken); // Chờ đến 6h sáng

                try
                {
                    var client = new HttpClient();
                    await client.PostAsync("http://10.220.130.119:9090/api/CheckList/send-daily-alerts", null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending daily alerts: {ex.Message}");
                }
            }
        }
    }

}
