using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Service;
using System.Globalization;

namespace WebApplication1.ViewComponents
{
    public class ForecastWeatherViewComponent : ViewComponent
    {
        private readonly WeatherService _weatherService;

        public ForecastWeatherViewComponent()
        {
            _weatherService = new WeatherService();
        }

        public async Task<IViewComponentResult> InvokeAsync(string? location)
        {
            location ??= "Prague";

            var forecastResponse = await _weatherService.GetForecastAsync(location, 3);

            var model = new ForecastWeatherViewModel
            {
                LocationName = forecastResponse.Location.Name,
                ForecastDays = forecastResponse.Forecast.Forecastday.Select(d => new ForecastDayViewModel
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

            ViewData["LocationName"] = model.LocationName;
            return View("Default", model); // Posíláme celý model
        }
    }
}
