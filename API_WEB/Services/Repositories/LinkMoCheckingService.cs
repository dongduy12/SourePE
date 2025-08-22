using API_WEB.Controllers.Repositories;
using API_WEB.ModelsDB;
using API_WEB.ModelsOracle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API_WEB.Services.Repositories
{
    public class LinkMoCheckingService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LinkMoCheckingService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(2);

        public LinkMoCheckingService(IServiceProvider serviceProvider, ILogger<LinkMoCheckingService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LinkMoCheckingService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var sqlContext = scope.ServiceProvider.GetRequiredService<CSDL_NE>();
                    var oracleContext = scope.ServiceProvider.GetRequiredService<OracleDbContext>();

                    var controller = new AutoExportController(sqlContext, oracleContext);
                    await controller.CheckLinkMoAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in LinkMoCheckingService: {ex.Message}");
                }

                await Task.Delay(_interval, stoppingToken);
            }

            _logger.LogInformation("LinkMoCheckingService stopped.");
        }
    }
}
