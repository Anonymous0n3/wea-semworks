using Microsoft.AspNetCore.Mvc;
using WebApplication1.Service;
using System.Threading.Tasks;

namespace WebApplication1.ViewComponents
{
    public class NewsWidgetViewComponent : ViewComponent
    {
        private readonly MqttNewsService _newsService;

        public NewsWidgetViewComponent(MqttNewsService newsService)
        {
            _newsService = newsService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Připojíme se k MQTT brokeru (pokud ještě nejsme)
            await _newsService.ConnectAsync();

            // Získáme všechny zprávy (teď už bez filtru na 7 dní)
            var newsList = _newsService.GetRecentMessages();

            return View(newsList);
        }
    }
}
