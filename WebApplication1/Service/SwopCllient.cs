using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WebApplication1.Service
{

    public class SwopClient : ISwopClient
    {
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public SwopClient(IConfiguration config, IHttpClientFactory clientFactory)
        {
            _http = clientFactory.CreateClient();

            var apiUrl = config["SWOP_API_URL"] ?? Environment.GetEnvironmentVariable("SWOP_API_URL");
            if (string.IsNullOrWhiteSpace(apiUrl))
                throw new ArgumentNullException("SWOP_API_URL není nastaven");

            _http.BaseAddress = new Uri(apiUrl);

            var apiKey = config["SWOP_API_KEY"] ?? Environment.GetEnvironmentVariable("SWOP_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentNullException("SWOP_API_KEY není nastaven");

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
        }

        public async Task<decimal> GetLatestRateAsync(string baseCurrencyIso, string quoteCurrencyIso)
        {
            var query = @"
            query($baseCurrency: String!, $quoteCurrencies: [String!]!) {
                latest(baseCurrency: $baseCurrency, quoteCurrencies: $quoteCurrencies) {
                    date
                    baseCurrency
                    quoteCurrency
                    quote
                }
            }";

            var body = new
            {
                query,
                variables = new { baseCurrency = baseCurrencyIso, quoteCurrencies = new[] { quoteCurrencyIso } }
            };

            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync("", content);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Console.WriteLine("RAW SWOP response (latest):");
            Console.WriteLine(json);

            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind == JsonValueKind.Null)
                throw new Exception("SWOP API nevrátilo data (data = null)");

            if (!data.TryGetProperty("latest", out var latestArray) || latestArray.ValueKind != JsonValueKind.Array || latestArray.GetArrayLength() == 0)
                throw new Exception("SWOP API nevrátilo aktuální kurz (latest = empty)");

            foreach (var item in latestArray.EnumerateArray())
            {
                if (item.GetProperty("quoteCurrency").GetString() == quoteCurrencyIso)
                    return item.GetProperty("quote").GetDecimal();
            }

            throw new Exception($"SWOP API nevrátilo kurz pro měnu {quoteCurrencyIso}");
        }

        public async Task<List<HistoricalPoint>> GetHistoricalRatesAsync(string baseCurrencyIso, string quoteCurrencyIso, HistoricalInterval interval)
        {
            var endDate = DateTime.UtcNow.Date;
            var startDate = interval == HistoricalInterval.Month ? endDate.AddDays(-30) : endDate.AddDays(-7);

            var result = new List<HistoricalPoint>();

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var query = @"
                query($date: Date!, $baseCurrency: String!, $quoteCurrencies: [String!]!) {
                    historical(date: $date, baseCurrency: $baseCurrency, quoteCurrencies: $quoteCurrencies) {
                        date
                        baseCurrency
                        quoteCurrency
                        quote
                    }
                }";

                var body = new
                {
                    query,
                    variables = new
                    {
                        date = date.ToString("yyyy-MM-dd"),  // posíláme čistý datum string
                        baseCurrency = baseCurrencyIso,
                        quoteCurrencies = new[] { quoteCurrencyIso }
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                var resp = await _http.PostAsync("", content);
                resp.EnsureSuccessStatusCode();

                var json = await resp.Content.ReadAsStringAsync();
                Console.WriteLine($"RAW SWOP response (historical {date:yyyy-MM-dd}):");
                Console.WriteLine(json);

                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind == JsonValueKind.Null)
                    continue;

                if (!data.TryGetProperty("historical", out var histArray) || histArray.ValueKind != JsonValueKind.Array || histArray.GetArrayLength() == 0)
                    continue;

                foreach (var item in histArray.EnumerateArray())
                {
                    if (item.GetProperty("quoteCurrency").GetString() != quoteCurrencyIso)
                        continue;

                    try
                    {
                        var dateStr = item.GetProperty("date").GetString();
                        if (string.IsNullOrWhiteSpace(dateStr))
                            continue;

                        result.Add(new HistoricalPoint
                        {
                            Timestamp = DateTime.ParseExact(dateStr, "yyyy-MM-dd", null),
                            Rate = item.GetProperty("quote").GetDecimal()
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Chyba při zpracování historického data {date:yyyy-MM-dd}: {ex.Message}");
                    }
                }
            }

            return result;
        }
        // --- nová metoda HealthCheck ---
        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                var rate = await GetLatestRateAsync("EUR", "USD");
                return rate > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
