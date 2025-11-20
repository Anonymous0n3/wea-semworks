using Quartz;
using WebApplication1.Models;

namespace WebApplication1.Service;

public class NewsQuartzJob : IJob
{
    private readonly MqttNewsService _mqttService;
    private readonly NewsRepository _repository;
    private readonly ILogger<NewsQuartzJob> _logger;

    private readonly string[] _categories = { "TRAVEL", "SPORTS" };

    public NewsQuartzJob(
        MqttNewsService mqttService,
        NewsRepository repository,
        ILogger<NewsQuartzJob> logger)
    {
        _mqttService = mqttService;
        _repository = repository;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogWarning("[NewsQuartzJob] 🕒 Job triggered");

        await _mqttService.ConnectAsync();

        var news = _repository.GetRandomByCategories(_categories);

        if (news != null)
        {
            _logger.LogInformation("[NewsQuartzJob] Publishing {Count} messages", news.Count);

            // Pozn.: Pokud máš novou verzi PublishNewsAsync(List<NewsMessage>)
            foreach (var item in news)
            {
                await _mqttService.PublishNewsAsync(item);
            }
        }

        _logger.LogWarning("[NewsQuartzJob] ✅ Job finished");
    }
}
