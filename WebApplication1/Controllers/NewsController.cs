using Microsoft.AspNetCore.Mvc;
using WebApplication1.Service;

namespace WebApplication1.Controllers
{
    public class NewsController : Controller
    {
        private readonly MqttNewsService _mqttService;

        public NewsController(MqttNewsService mqttService)
        {
            _mqttService = mqttService;
        }

        public IActionResult Index(string[]? categories)
        {
            var all = _mqttService.GetRecentMessages();

            if (categories != null && categories.Length > 0)
                all = all.Where(n => categories.Contains(n.Category, StringComparer.OrdinalIgnoreCase));

            // Seznam všech kategorií pro filtr
            var allCategories = new[]
            {
                "POLITICS", "PARENTS", "WELLNESS", "HOME & LIVING",
                "ENTERTAINMENT", "BLACK VOICES", "TRAVEL", "SPORTS",
                "STYLE & BEAUTY", "COMEDY", "PARENTING", "BUSINESS",
                "HEALTHY LIVING", "FOOD & DRINK"
            };

            ViewBag.Categories = allCategories;
            ViewBag.Selected = categories ?? Array.Empty<string>();

            return View(all);
        }
    }
}
