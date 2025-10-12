using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace YourProject.ViewComponents
{
    public class WeatherWidgetViewComponent : ViewComponent
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public WeatherWidgetViewComponent(IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = new HttpClient();
        }

        public async Task<IViewComponentResult> InvokeAsync(string? location = null)
        {
            var apiKey = _configuration["WeatherApi:ApiKey"];
            var baseUrl = _configuration["WeatherApi:BaseUrl"] ?? "https://api.weatherapi.com/v1";
            location ??= "auto:ip"; // když uživatel nezadá, vezmeme automaticky podle IP

            // Volání API
            var url = $"{baseUrl}/forecast.json?key={apiKey}&q={location}&days=3&aqi=yes";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return View(new WeatherViewModel { LocationName = "N/A" });
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            var json = await JsonDocument.ParseAsync(stream);

            var locationObj = json.RootElement.GetProperty("location");
            var current = json.RootElement.GetProperty("current");
            var forecast = json.RootElement.GetProperty("forecast").GetProperty("forecastday");

            double tempC = current.GetProperty("temp_c").GetDouble();
            double humidity = current.GetProperty("humidity").GetDouble();
            double dewPoint = tempC - ((100 - humidity) / 5.0);

            var vm = new WeatherViewModel
            {
                LocationName = locationObj.GetProperty("name").GetString() ?? "Unknown",
                ConditionText = current.GetProperty("condition").GetProperty("text").GetString() ?? "",
                ConditionIcon = current.GetProperty("condition").GetProperty("icon").GetString() ?? "",
                TemperatureC = tempC,
                TemperatureF = current.GetProperty("temp_f").GetDouble(),
                Humidity = humidity,
                WindKph = current.GetProperty("wind_kph").GetDouble(),
                WindDir = current.GetProperty("wind_dir").GetString() ?? "",
                UvIndex = current.GetProperty("uv").GetDouble(),
                DewPoint = dewPoint,
                AirQualityIndex = (int)Math.Round(current.GetProperty("air_quality").GetProperty("us-epa-index").GetDouble())
            };

            foreach (var day in forecast.EnumerateArray())
            {
                vm.Forecast.Add(new ForecastDay
                {
                    Date = DateTime.Parse(day.GetProperty("date").GetString() ?? ""),
                    Icon = day.GetProperty("day").GetProperty("condition").GetProperty("icon").GetString() ?? "",
                    MaxTempC = day.GetProperty("day").GetProperty("maxtemp_c").GetDouble(),
                    MinTempC = day.GetProperty("day").GetProperty("mintemp_c").GetDouble(),
                    Precipitation = day.GetProperty("day").GetProperty("daily_chance_of_rain").GetDouble(),
                    UvIndex = day.GetProperty("day").GetProperty("uv").GetDouble(),
                    AirQualityIndex = vm.AirQualityIndex
                });
            }

            return View(vm);
        }
    }

    // ===== MODEL =====
    public class WeatherViewModel
    {
        public string LocationName { get; set; } = "";
        public string ConditionText { get; set; } = "";
        public string ConditionIcon { get; set; } = "";
        public double TemperatureC { get; set; }
        public double TemperatureF { get; set; }
        public double Humidity { get; set; }
        public double WindKph { get; set; }
        public string WindDir { get; set; } = "";
        public double UvIndex { get; set; }
        public double DewPoint { get; set; }
        public int AirQualityIndex { get; set; }
        public string AirQualityLevel { get; set; } = "";
        public List<ForecastDay> Forecast { get; set; } = new();
    }

    public class ForecastDay
    {
        public DateTime Date { get; set; }
        public string Icon { get; set; } = "";
        public double MaxTempC { get; set; }
        public double MinTempC { get; set; }
        public double Precipitation { get; set; }
        public double UvIndex { get; set; }
        public int AirQualityIndex { get; set; }
    }
}
