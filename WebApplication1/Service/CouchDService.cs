using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using BCrypt.Net;
using WebApplication1.Models;
using WebApplication1.Models.Data;

namespace WebApplication1.Service
{
    public class CouchDbService
    {
        private readonly HttpClient _client;
        private readonly string _dbName = "swopdb";
        private readonly string _couchBase;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly JwtOptions _jwtOptions;

        public CouchDbService(HttpClient client, JwtOptions jwtOptions, IConfiguration config)
        {
            _client = client;
            _couchBase = config["COUCHDB_URL"] ?? "http://localhost:5984";

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var user = config["COUCHDB_USER"] ?? "admin";
            var pass = config["COUCHDB_PASSWORD"] ?? "adminpassword";

            if (!string.IsNullOrEmpty(user))
            {
                var byteArray = Encoding.ASCII.GetBytes($"{user}:{pass}");
                _client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }

            // Automatická inicializace databáze měn
            Task.Run(async () =>
            {
                try
                {
                    var seedCurrencies = new List<Currency>
                    {
                        new Currency { Id = "EUR", Name = "Euro" },
                        new Currency { Id = "USD", Name = "US Dollar" },
                        new Currency { Id = "GBP", Name = "British Pound" },
                        new Currency { Id = "CHF", Name = "Swiss Franc" },
                        new Currency { Id = "JPY", Name = "Japanese Yen" },
                        new Currency { Id = "AUD", Name = "Australian Dollar" },
                        new Currency { Id = "CAD", Name = "Canadian Dollar" },
                        new Currency { Id = "CNY", Name = "Chinese Yuan" },
                        new Currency { Id = "SEK", Name = "Swedish Krona" },
                        new Currency { Id = "NZD", Name = "New Zealand Dollar" },
                        new Currency { Id = "NOK", Name = "Norwegian Krone" },
                        new Currency { Id = "MXN", Name = "Mexican Peso" },
                        new Currency { Id = "SGD", Name = "Singapore Dollar" },
                        new Currency { Id = "HKD", Name = "Hong Kong Dollar" },
                        new Currency { Id = "KRW", Name = "South Korean Won" },
                        new Currency { Id = "TRY", Name = "Turkish Lira" },
                        new Currency { Id = "RUB", Name = "Russian Ruble" },
                        new Currency { Id = "INR", Name = "Indian Rupee" },
                        new Currency { Id = "BRL", Name = "Brazilian Real" },
                        new Currency { Id = "ZAR", Name = "South African Rand" }
                    };

                    await EnsureCurrenciesDbAndSeedAsync(seedCurrencies);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Chyba při inicializaci databáze měn: {ex.Message}");
                }
            });
        }

        // --- Původní metody ---
        public async Task EnsureDbExistsAsync()
        {
            var dbUri = $"{_couchBase}/{_dbName}";
            var head = await _client.GetAsync(dbUri);

            if (head.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var create = await _client.PutAsync(dbUri, null);
                if (!create.IsSuccessStatusCode)
                {
                    var error = await create.Content.ReadAsStringAsync();
                    throw new Exception($"Nepodařilo se vytvořit databázi '{_dbName}': {create.StatusCode} {error}");
                }
            }
            else if (!head.IsSuccessStatusCode)
            {
                var error = await head.Content.ReadAsStringAsync();
                throw new Exception($"Kontrola databáze '{_dbName}' selhala: {head.StatusCode} {error}");
            }
        }

        public async Task<List<Currency>> GetAllCurrenciesAsync()
        {
            var resp = await _client.GetAsync($"{_couchBase}/{_dbName}/_all_docs?include_docs=true");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var list = new List<Currency>();
            foreach (var row in doc.RootElement.GetProperty("rows").EnumerateArray())
            {
                if (row.TryGetProperty("doc", out var d))
                {
                    var item = JsonSerializer.Deserialize<Currency>(d.GetRawText(), _jsonOptions);
                    if (item != null) list.Add(item);
                }
            }
            return list;
        }

        public async Task<Currency?> GetCurrencyAsync(string isoCode)
        {
            var id = Uri.EscapeDataString(isoCode.ToUpperInvariant());
            var resp = await _client.GetAsync($"{_couchBase}/{_dbName}/{id}");
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            var cur = JsonSerializer.Deserialize<Currency>(json, _jsonOptions);
            return cur;
        }

        public async Task<HttpResponseMessage> PostDocumentAsync<T>(T doc)
        {
            var json = JsonSerializer.Serialize(doc, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await _client.PostAsync($"{_couchBase}/{_dbName}", content);
        }

        public async Task<List<T>> GetAllDocumentsAsync<T>()
        {
            var resp = await _client.GetAsync($"{_couchBase}/{_dbName}/_all_docs?include_docs=true");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var list = new List<T>();
            foreach (var row in doc.RootElement.GetProperty("rows").EnumerateArray())
            {
                if (row.TryGetProperty("doc", out var d))
                {
                    var item = JsonSerializer.Deserialize<T>(d.GetRawText(), _jsonOptions);
                    if (item != null) list.Add(item);
                }
            }
            return list;
        }

        public async Task SeedCurrenciesAsync(List<Currency> currencies)
        {
            var existing = await GetAllCurrenciesAsync();
            if (existing.Count > 0) return; // již seedováno

            foreach (var c in currencies)
            {
                var json = JsonSerializer.Serialize(c, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await _client.PutAsync($"{_couchBase}/{_dbName}/{c.Id}", content);
                if (!res.IsSuccessStatusCode)
                {
                    var body = await res.Content.ReadAsStringAsync();
                    Console.WriteLine($"Seed error for {c.Id}: {res.StatusCode} {body}");
                }
            }
        }

        // --- Nová metoda pro automatické zajištění DB měn a seedování ---
        public async Task EnsureCurrenciesDbAndSeedAsync(List<Currency> seedCurrencies)
        {
            var dbName = "currenciesdb";
            var dbUri = $"{_couchBase}/{dbName}";

            // 1) Zkontrolovat existenci DB
            var head = await _client.GetAsync(dbUri);
            if (head.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var create = await _client.PutAsync(dbUri, null);
                if (!create.IsSuccessStatusCode)
                {
                    var error = await create.Content.ReadAsStringAsync();
                    throw new Exception($"Nepodařilo se vytvořit databázi '{dbName}': {create.StatusCode} {error}");
                }
                Console.WriteLine($"Databáze '{dbName}' byla vytvořena.");
            }
            else if (!head.IsSuccessStatusCode)
            {
                var error = await head.Content.ReadAsStringAsync();
                throw new Exception($"Kontrola databáze '{dbName}' selhala: {head.StatusCode} {error}");
            }

            // 2) Zkontrolovat, zda jsou v DB dokumenty
            var resp = await _client.GetAsync($"{dbUri}/_all_docs?limit=1");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var totalRows = doc.RootElement.GetProperty("total_rows").GetInt32();

            if (totalRows > 0)
            {
                Console.WriteLine($"Databáze '{dbName}' již obsahuje data, seed se neprovádí.");
                return;
            }

            // 3) Nasypat seed data
            foreach (var c in seedCurrencies)
            {
                var content = new StringContent(JsonSerializer.Serialize(c, _jsonOptions), Encoding.UTF8, "application/json");
                var putResp = await _client.PutAsync($"{dbUri}/{Uri.EscapeDataString(c.Id)}", content);
                if (!putResp.IsSuccessStatusCode)
                {
                    var body = await putResp.Content.ReadAsStringAsync();
                    Console.WriteLine($"Seed error for {c.Id}: {putResp.StatusCode} {body}");
                }
            }

            Console.WriteLine($"Seed dat pro databázi '{dbName}' dokončen.");
        }
    }
}
