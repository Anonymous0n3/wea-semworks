using WebApplication1.Models;
using System.Text.Json;

namespace WebApplication1.Service
{
    public class NewsRepository
    {
        private readonly List<NewsMessage> _allNews;
        private readonly ILogger<NewsRepository> _logger;

        public NewsRepository(ILogger<NewsRepository> logger)
        {
            _logger = logger;
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "filtered_news.json");

            if (!File.Exists(path))
            {
                _logger.LogWarning($"[NewsRepository] ❌ Soubor {path} nenalezen!");
                _allNews = new List<NewsMessage>();
                return;
            }

            var json = File.ReadAllText(path);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            _allNews = JsonSerializer.Deserialize<List<NewsMessage>>(json, options)
                        ?? new List<NewsMessage>();
        }

        public List<NewsMessage> GetRandomByCategories(string[] categories)
        {
            var rnd = new Random();
            var result = new List<NewsMessage>();

            foreach (var category in categories)
            {
                var filtered = _allNews.Where(n => n.Category == category).ToList();
                if (filtered.Count == 0)
                {
                    result.Add(null); // nebo vynechej, pokud nechceš null
                    continue;
                }

                var randomNews = filtered[rnd.Next(filtered.Count)];
                result.Add(randomNews);
            }

            return result;
        }

    }
}
