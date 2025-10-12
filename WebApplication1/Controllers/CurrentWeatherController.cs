using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Service;

namespace WebApplication1.Controllers
{
    public class CurrentWeatherController : Controller
    {
        private readonly WeatherService _weatherService;

        public CurrentWeatherController()
        {
            _weatherService = new WeatherService();
        }

        // Akce pro zobrazení aktuálního počasí
        public async Task<IActionResult> Index(string location = "Prague")
        {
            var current = await _weatherService.GetCurrentWeatherAsync(location);
            if (current == null)
                return View("Error");

            var model = new CurrentWeatherViewModel
            {
                LocationName = current.Location.Name,
                TempC = current.Current.TempC,
                TempF = current.Current.TempF,
                ConditionText = current.Current.Condition.Text,
                ConditionIcon = current.Current.Condition.Icon,
                Humidity = current.Current.Humidity,
                WindKph = current.Current.WindKph,
                WindDir = current.Current.WindDir,
                DewPoint = current.Current.DewPoint,
                Uv = current.Current.Uv,
                UsEpaIndex = current.Current.AirQuality?.UsEpaIndex
            };

            return View(model); // Views/CurrentWeather/Index.cshtml
        }
    }
}
