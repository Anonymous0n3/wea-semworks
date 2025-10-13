using Microsoft.AspNetCore.Mvc;
using System.Globalization;
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
                LocationName = forecast.Location.Name,
                ForecastDays = forecast.Forecast.Forecastday.Select(d => new ForecastDayViewModel
                {
                    Date = d.Date,
                    ConditionText = d.Day.Condition.Text,
                    ConditionIcon = d.Day.Condition.Icon,
                    MaxTempC = d.Day.MaxtempC,
                    MinTempC = d.Day.MintempC,
                    ChanceOfRain = (int)d.Day.DailyChanceOfRain,
                    Uv = d.Day.Uv,
                    UsEpaIndex = d.Day.AirQuality?.UsEpaIndex,
                    Hourly = d.Hourly.Select(h => new HourlyForecastViewModel
                    {
                        Hour = DateTime.ParseExact(h.Time, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                                       .ToString("HH:mm"),
                        TempC = h.TempC,
                        TempF = h.TempF
                    }).ToList()
                }).ToList()
            };

            return View(model); // Views/ForecastWeather/Index.cshtml
        }
    }
}
