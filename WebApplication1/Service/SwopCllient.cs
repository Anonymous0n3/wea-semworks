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

            // Ofiko dle docs: Authorization: ApiKey <key>
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

            using var content = new StringContent(JsonSerializer.Serialize(body, _jsonOptions), Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync("", content);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

            if (doc.RootElement.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array && errs.GetArrayLength() > 0)
                throw new Exception($"GraphQL errors: {errs}");

            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                throw new Exception("SWOP API nevrátilo data (data je null nebo není objekt)");

            if (!data.TryGetProperty("latest", out var latestArray) || latestArray.ValueKind != JsonValueKind.Array || latestArray.GetArrayLength() == 0)
                throw new Exception("SWOP API nevrátilo aktuální kurz (latest je prázdné)");

            foreach (var item in latestArray.EnumerateArray())
            {
                if (string.Equals(item.GetProperty("quoteCurrency").GetString(), quoteCurrencyIso, StringComparison.OrdinalIgnoreCase))
                    return item.GetProperty("quote").GetDecimal();
            }

            throw new Exception($"SWOP API nevrátilo kurz pro měnu {quoteCurrencyIso}");
        }

        public async Task<List<HistoricalPoint>> GetHistoricalRatesAsync(string baseCurrencyIso, string quoteCurrencyIso, HistoricalInterval interval)
        {
            var endDate = DateTime.UtcNow.Date;
            var startDate = interval == HistoricalInterval.Month ? endDate.AddDays(-30) : endDate.AddDays(-7);

            // --- 1) Zkusíme timeSeries (nejrychlejší a úsporné) ---
            try
            {
                var tsQuery = @"
                query TimeSeries($from: Date!, $to: Date!, $base: String!, $quotes: [String!]!) {
                  timeSeries(dateFrom: $from, dateTo: $to, baseCurrency: $base, quoteCurrencies: $quotes) {
                    baseCurrency
                    quoteCurrency
                    rates {
                      date
                      quote
                    }
                    quotes {
                      date
                      quote
                    }
                  }
                }";

                var tsBody = new
                {
                    query = tsQuery,
                    variables = new
                    {
                        from = startDate.ToString("yyyy-MM-dd"),
                        to = endDate.ToString("yyyy-MM-dd"),
                        @base = baseCurrencyIso,
                        quotes = new[] { quoteCurrencyIso }
                    }
                };

                using var tsContent = new StringContent(JsonSerializer.Serialize(tsBody, _jsonOptions), Encoding.UTF8, "application/json");
                using var tsResp = await _http.PostAsync("", tsContent);
                tsResp.EnsureSuccessStatusCode();

                using var tsDoc = JsonDocument.Parse(await tsResp.Content.ReadAsStringAsync());

                if (tsDoc.RootElement.TryGetProperty("errors", out var tsErrs) && tsErrs.ValueKind == JsonValueKind.Array && tsErrs.GetArrayLength() > 0)
                    throw new InvalidOperationException("timeSeries errors");

                if (!tsDoc.RootElement.TryGetProperty("data", out var tsData) || tsData.ValueKind != JsonValueKind.Object)
                    throw new InvalidOperationException("timeSeries data null");

                if (tsData.TryGetProperty("timeSeries", out var tsArr) &&
                    tsArr.ValueKind == JsonValueKind.Array &&
                    tsArr.GetArrayLength() > 0)
                {
                    var series = tsArr.EnumerateArray().FirstOrDefault();
                    if (series.ValueKind != JsonValueKind.Undefined)
                    {
                        // některé verze mají "rates", jiné "quotes"
                        var points = new List<HistoricalPoint>();

                        if (series.TryGetProperty("rates", out var rates) && rates.ValueKind == JsonValueKind.Array)
                            points.AddRange(ParseDateQuoteArray(rates));

                        if (series.TryGetProperty("quotes", out var quotes) && quotes.ValueKind == JsonValueKind.Array)
                            points.AddRange(ParseDateQuoteArray(quotes));

                        if (points.Count > 0)
                            return points
                                .GroupBy(p => p.Timestamp.Date) // kdyby se duplikovalo
                                .Select(g => g.First())
                                .OrderBy(p => p.Timestamp)
                                .ToList();
                    }
                }
            }
            catch
            {
                // ignorujeme a jdeme na fallback
            }

            // --- 2) FALLBACK: per-day historical, jen pracovní dny (Po–Pá) ---
            var result = new List<HistoricalPoint>();

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var dow = date.DayOfWeek;
                if (dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday)
                    continue; // víkendy většinou nemají kurzy

                var histQuery = @"
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
                    query = histQuery,
                    variables = new
                    {
                        date = date.ToString("yyyy-MM-dd"),
                        baseCurrency = baseCurrencyIso,
                        quoteCurrencies = new[] { quoteCurrencyIso }
                    }
                };

                using var content = new StringContent(JsonSerializer.Serialize(body, _jsonOptions), Encoding.UTF8, "application/json");
                using var resp = await _http.PostAsync("", content);
                if (!resp.IsSuccessStatusCode) continue;

                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

                if (doc.RootElement.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array && errs.GetArrayLength() > 0)
                    continue;

                if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                    continue;

                if (!data.TryGetProperty("historical", out var histArray) || histArray.ValueKind != JsonValueKind.Array || histArray.GetArrayLength() == 0)
                    continue;

                foreach (var item in histArray.EnumerateArray())
                {
                    if (!string.Equals(item.GetProperty("quoteCurrency").GetString(), quoteCurrencyIso, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var dateStr = item.GetProperty("date").GetString();
                    if (!DateTime.TryParse(dateStr, out var dt)) continue;

                    result.Add(new HistoricalPoint
                    {
                        Timestamp = dt,
                        Rate = item.GetProperty("quote").GetDecimal()
                    });
                }
            }

            return result.OrderBy(p => p.Timestamp).ToList();
        }

        private static IEnumerable<HistoricalPoint> ParseDateQuoteArray(JsonElement arr)
        {
            foreach (var r in arr.EnumerateArray())
            {
                var dateStr = r.TryGetProperty("date", out var d) ? d.GetString() : null;
                if (string.IsNullOrWhiteSpace(dateStr) || !DateTime.TryParse(dateStr, out var dt)) continue;

                if (!r.TryGetProperty("quote", out var q)) continue;

                yield return new HistoricalPoint
                {
                    Timestamp = dt,
                    Rate = q.GetDecimal()
                };
            }
        }

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
