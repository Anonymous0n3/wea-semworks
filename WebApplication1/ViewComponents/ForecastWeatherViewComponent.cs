using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Service;

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
            if (string.IsNullOrEmpty(location))
            {
                location = "Prague"; // default
            }

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
                    ChanceOfRain = d.Day.DailyChanceOfRain,
                    Uv = d.Day.Uv,
                    UsEpaIndex = d.Day.AirQuality?.UsEpaIndex
                }).ToList()
            };

            ViewData["LocationName"] = model.LocationName;
            return View("Default", model.ForecastDays);
        }
    }
}
