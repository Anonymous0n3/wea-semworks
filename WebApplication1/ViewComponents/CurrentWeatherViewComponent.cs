using Microsoft.AspNetCore.Mvc;
using WebApplication1.Service;

namespace WebApplication1.ViewComponents
{
    public class CurrentWeatherViewComponent : ViewComponent
    {
        private readonly WeatherService _weatherService;

        public CurrentWeatherViewComponent(WeatherService weatherService)
        {
            _weatherService = weatherService;
        }

        public async Task<IViewComponentResult> InvokeAsync(string? location = null)
        {
            location ??= "Prague";

            var currentWeather = await _weatherService.GetCurrentWeatherAsync(location);

            if (currentWeather?.Current == null)
            {
                return View(new Models.CurrentWeatherViewModel { LocationName = "N/A" });
            }

            var current = currentWeather.Current;

            var vm = new Models.CurrentWeatherViewModel
            {
                LocationName = currentWeather.Location?.Name ?? "Unknown",
                ConditionText = current.Condition?.Text ?? "",
                ConditionIcon = current.Condition?.Icon ?? "",
                TempC = current.TempC,
                TempF = current.TempF,
                Humidity = current.Humidity,
                WindKph = current.WindKph,
                WindDir = current.WindDir ?? "",
                DewPoint = current.DewPoint,
                Uv = current.Uv,
                AirQualityIndex = current.AirQuality?.UsEpaIndex ?? 0,
                AirQualityLevel = (current.AirQuality?.UsEpaIndex ?? 0) switch
                {
                    1 => "Good",
                    2 => "Moderate",
                    3 => "Unhealthy for sensitive groups",
                    4 => "Unhealthy",
                    5 => "Very Unhealthy",
                    _ => "Unknown"
                }
            };

            return View(vm);
        }
    }
}
