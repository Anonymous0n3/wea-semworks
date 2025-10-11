using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Service;

namespace WebApplication1.Controllers
{
    public class ForecastWeatherController : Controller
    {
        private readonly WeatherService _weatherService;

        public ForecastWeatherController()
        {
            _weatherService = new WeatherService();
        }

        // Akce pro zobrazení 3denní předpovědi
        public async Task<IActionResult> Index(string location = "Prague")
        {
            var forecast = await _weatherService.GetForecastAsync(location, 3);
            if (forecast == null)
                return View("Error");

            var model = new ForecastWeatherViewModel
            {
                LocationName = forecast.Location.Name
            };

            foreach (var day in forecast.Forecast.Forecastday)
            {
                model.ForecastDays.Add(new ForecastDayViewModel
                {
                    Date = day.Date,
                    ConditionText = day.Day.Condition.Text,
                    ConditionIcon = day.Day.Condition.Icon,
                    MaxTempC = day.Day.MaxtempC,
                    MinTempC = day.Day.MintempC,
                    ChanceOfRain = day.Day.DailyChanceOfRain,
                    Uv = day.Day.Uv,
                    UsEpaIndex = day.Day.AirQuality?.UsEpaIndex
                });
            }

            return View(model); // Views/ForecastWeather/Index.cshtml
        }
    }
}
