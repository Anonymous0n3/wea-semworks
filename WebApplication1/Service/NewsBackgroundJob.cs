using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using WebApplication1.Models;

namespace WebApplication1.Service
{
    public class NewsBackgroundJob : BackgroundService
    {
        private readonly MqttNewsService _mqttService;
        private readonly NewsRepository _repository;

        private readonly ILogger<NewsBackgroundJob> _logger;

        // Skupina 4 má TRAVEL + SPORTS
        private readonly string[] _categories = { "TRAVEL", "SPORTS" };

        public NewsBackgroundJob(MqttNewsService mqttService, NewsRepository repository, ILogger<NewsBackgroundJob> logger)
        {
            _logger = logger;
            _mqttService = mqttService;
            _repository = repository;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // nejprve připojení MQTT klienta
            await _mqttService.ConnectAsync();

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("[NewsBackgroundJob]  🕒 Background job running...");

                    var news = _repository.GetRandomByCategories(_categories);
                    if (news != null)
                    {
                    foreach (var item in news)
                    {
                        await _mqttService.PublishNewsAsync(item);
                    }
                    }

                _logger.LogWarning("[NewsBackgroundJob] ✅ News published, waiting 5 hours...");
                await Task.Delay(TimeSpan.FromHours(5), stoppingToken);
            }
        }
    }
}
