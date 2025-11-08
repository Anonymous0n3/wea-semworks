using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WebApplication1.Service
{
    public class MqttBackgroundService : IHostedService
    {
        private readonly MqttNewsService _mqttService;
        private readonly ILogger<MqttBackgroundService> _logger;

        public MqttBackgroundService(MqttNewsService mqttService, ILogger<MqttBackgroundService> logger)
        {
            _mqttService = mqttService;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[MqttBackgroundService] 🚀 Starting MQTT background service...");
            await _mqttService.ConnectAsync(); // připojení k brokeru při startu
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[MqttBackgroundService] 🛑 Stopping MQTT background service...");
            return Task.CompletedTask;
        }
    }
}
