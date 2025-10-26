using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;
using DotNetEnv;


namespace WebApplication1.Service
{
    public class WeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseUrl;

        public WeatherService()
        {
            Env.Load(); // načte .env soubor
            _apiKey = Env.GetString("WEATHER_API_KEY") ?? throw new ArgumentNullException("WEATHER_API_KEY chybí v .env");
            _baseUrl = Env.GetString("WEATHER_API_BASEURL") ?? "http://api.weatherapi.com/v1";

            _httpClient = new HttpClient();
        }

        // --- Current weather ---
        public async Task<CurrentWeatherResponse> GetCurrentWeatherAsync(string location)
        {
            string url = $"{_baseUrl}/current.json?key={_apiKey}&q={location}&aqi=yes";
            var response = await _httpClient.GetStringAsync(url);
            var weather = JsonSerializer.Deserialize<CurrentWeatherResponse>(response, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // dopočítat rosný bod, pokud není přímo z API
            if (weather?.Current != null)
            {
                weather.Current.DewPoint = CalculateDewPoint(weather.Current.TempC, weather.Current.Humidity);
            }

            return weather;
        }

        // --- Forecast ---
        public async Task<ForecastResponse> GetForecastAsync(string location, int days = 3)
        {
            string url = $"{_baseUrl}/forecast.json?key={_apiKey}&q={location}&days={days}&aqi=yes&alerts=no";
            var response = await _httpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<ForecastResponse>(response, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        // --- Search locations / autocomplete ---
        public async Task<List<SearchLocation>> SearchLocationAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<SearchLocation>();

            try
            {
                string url = $"{_baseUrl}/search.json?key={_apiKey}&q={Uri.EscapeDataString(query)}";
                var response = await _httpClient.GetStringAsync(url);

                var locations = JsonSerializer.Deserialize<List<SearchLocation>>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return locations ?? new List<SearchLocation>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SearchLocationAsync error: {ex.Message}");
                return new List<SearchLocation>();
            }
        }

        // --- Výpočet rosného bodu ---
        private double CalculateDewPoint(double tempC, double humidity)
        {
            double a = 17.27;
            double b = 237.7;
            double alpha = ((a * tempC) / (b + tempC)) + Math.Log(humidity / 100.0);
            return (b * alpha) / (a - alpha);
        }

        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                // Zavoláme current weather pro testovací lokaci "London"
                string url = $"{_baseUrl}/current.json?key={_apiKey}&q=London&aqi=no";
                var response = await _httpClient.GetAsync(url);

                // Pokud HTTP status code je 200 OK, API je zdravé
                return response.IsSuccessStatusCode;
            }
            catch
            {
                // Jakákoliv chyba = API nedostupné
                return false;
            }
        }

    }

    // ----------------- DTO Třídy -----------------

    public class CurrentWeatherResponse
    {
        [JsonPropertyName("location")]
        public Location Location { get; set; }

        [JsonPropertyName("current")]
        public CurrentWeather Current { get; set; }
    }

    public class ForecastResponse
    {
        [JsonPropertyName("location")]
        public Location Location { get; set; }

        [JsonPropertyName("forecast")]
        public Forecast Forecast { get; set; }
    }

    public class Forecast
    {
        [JsonPropertyName("forecastday")]
        public List<ForecastDay> Forecastday { get; set; }
    }

    public class ForecastDay
    {
        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("day")]
        public Day Day { get; set; }

        [JsonPropertyName("hour")]
        public List<Hour> Hourly { get; set; }   // ← přidáno
    }

    public class Hour
    {
        [JsonPropertyName("time")]
        public string Time { get; set; }

        [JsonPropertyName("temp_c")]
        public double TempC { get; set; }

        [JsonPropertyName("temp_f")]
        public double TempF { get; set; }

        [JsonPropertyName("condition")]
        public Condition Condition { get; set; }

        [JsonPropertyName("chance_of_rain")]
        public double ChanceOfRain { get; set; }

        [JsonPropertyName("uv")]
        public double Uv { get; set; }
    }

    public class Day
    {
        [JsonPropertyName("maxtemp_c")]
        public double MaxtempC { get; set; }

        [JsonPropertyName("mintemp_c")]
        public double MintempC { get; set; }

        [JsonPropertyName("condition")]
        public Condition Condition { get; set; }

        [JsonPropertyName("daily_chance_of_rain")]
        public double DailyChanceOfRain { get; set; }

        [JsonPropertyName("uv")]
        public double Uv { get; set; }

        [JsonPropertyName("air_quality")]
        public AirQuality AirQuality { get; set; }
    }

    public class CurrentWeather
    {
        [JsonPropertyName("temp_c")]
        public double TempC { get; set; }

        [JsonPropertyName("temp_f")]
        public double TempF { get; set; }

        [JsonPropertyName("humidity")]
        public int Humidity { get; set; }

        [JsonPropertyName("wind_kph")]
        public double WindKph { get; set; }

        [JsonPropertyName("wind_dir")]
        public string WindDir { get; set; }

        [JsonPropertyName("condition")]
        public Condition Condition { get; set; }

        [JsonPropertyName("uv")]
        public double Uv { get; set; }

        [JsonPropertyName("dewpoint_c")]
        public double DewPoint { get; set; }

        [JsonPropertyName("air_quality")]
        public AirQuality AirQuality { get; set; }
    }

    public class Condition
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("icon")]
        public string Icon { get; set; }

        [JsonPropertyName("code")]
        public int Code { get; set; }
    }

    public class AirQuality
    {
        [JsonPropertyName("co")]
        public double CO { get; set; }

        [JsonPropertyName("o3")]
        public double O3 { get; set; }

        [JsonPropertyName("no2")]
        public double NO2 { get; set; }

        [JsonPropertyName("so2")]
        public double SO2 { get; set; }

        [JsonPropertyName("pm2_5")]
        public double PM25 { get; set; }

        [JsonPropertyName("pm10")]
        public double PM10 { get; set; }

        [JsonPropertyName("us-epa-index")]
        public int UsEpaIndex { get; set; }

        [JsonPropertyName("gb-defra-index")]
        public int GbDefraIndex { get; set; }
    }

    public class Location
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("region")]
        public string Region { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }

        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lon")]
        public double Lon { get; set; }

        [JsonPropertyName("tz_id")]
        public string TzId { get; set; }

        [JsonPropertyName("localtime")]
        public string Localtime { get; set; }
    }

    public class SearchLocation
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("region")]
        public string Region { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }

        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lon")]
        public double Lon { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}
