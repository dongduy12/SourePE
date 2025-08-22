using API_WEB.Controllers.Repositories;
using API_WEB.ModelsDB;
using API_WEB.ModelsOracle;

namespace API_WEB.Services.Repositories
{
    public class AutoExportBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AutoExportBackgroundService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(30); // Thời gian chờ giữa các lần chạy

        public AutoExportBackgroundService(IServiceProvider serviceProvider, ILogger<AutoExportBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AutoExportBackgroundService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var sqlContext = scope.ServiceProvider.GetRequiredService<CSDL_NE>();
                        var oracleContext = scope.ServiceProvider.GetRequiredService<OracleDbContext>();

                        var autoExportController = new AutoExportController(sqlContext, oracleContext);

                        var result = await autoExportController.AutoExport(); // Gọi AutoExport
                        _logger.LogInformation($"AutoExport executed. Result: {result}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in AutoExportBackgroundService: {ex.Message}");
                }

                await Task.Delay(_interval, stoppingToken); // Chờ trước khi thực hiện lần tiếp theo
            }

            _logger.LogInformation("AutoExportBackgroundService stopped.");
        }
    }
}
