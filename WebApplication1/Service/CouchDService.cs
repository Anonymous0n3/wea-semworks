using BCrypt.Net;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebApplication1.Controllers;
using WebApplication1.Models;
using WebApplication1.Models.Data;

namespace WebApplication1.Service
{
    public class CouchDbService
    {
        private readonly HttpClient _client;
        private readonly string _dbName = "helloworld";
        private readonly string _couchBase;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly JwtOptions _jwtOptions;
        private readonly ILogger<CouchDbService> _logger;

        public CouchDbService(HttpClient client, JwtOptions jwtOptions, IConfiguration config, ILogger<CouchDbService> logger)
        {
            _client = client;
            _jwtOptions = jwtOptions;
            _couchBase = config["COUCHDB_URL"] ?? "http://couchdb:5987";
            _logger = logger;

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
        }

        public string GenerateJwtForExistingUser(UserDoc user)
        {
            return GenerateJwtToken(user);
        }

        // ---------------------------
        // CouchDB základní operace
        // ---------------------------
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

        public async Task<HttpResponseMessage> GetDocumentAsync(string id)
        {
            return await _client.GetAsync($"{_couchBase}/{_dbName}/{id}");
        }

        public async Task<HttpResponseMessage> PostDocumentAsync<T>(T doc)
        {
            var json = JsonSerializer.Serialize(doc, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await _client.PostAsync($"{_couchBase}/{_dbName}", content);
        }

        public async Task<List<HelloDoc>> GetAllDocumentsAsync()
        {
            var resp = await _client.GetAsync($"{_couchBase}/{_dbName}/_all_docs?include_docs=true");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var list = new List<HelloDoc>();
            foreach (var row in doc.RootElement.GetProperty("rows").EnumerateArray())
            {
                if (row.TryGetProperty("doc", out var d))
                {
                    var item = JsonSerializer.Deserialize<HelloDoc>(d.GetRawText(), _jsonOptions);
                    if (item != null) list.Add(item);
                }
            }
            return list;
        }

        // ---------------------------
        // Autentizace a registrace
        // ---------------------------
        public async Task<UserDoc?> GetUserByEmailAsync(string email)
        {
            var encodedId = Uri.EscapeDataString(email);
            var url = $"{_couchBase}/{_dbName}/{encodedId}";

            try
            {
                var resp = await _client.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;

                var json = await resp.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<UserDoc>(json, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[CouchDB] Chyba při GET user: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> RegisterUserAsync(string name, string email, string password)
        {
            var existing = await GetUserByEmailAsync(email);
            if (existing != null) return false;

            var hash = BCrypt.Net.BCrypt.HashPassword(password);

            var user = new UserDoc
            {
                _id = email,
                Type = "user",
                Name = name,
                Email = email,
                PasswordHash = hash,
                OpenWidgets = new List<UserWidgetState>()
            };

            var encodedId = Uri.EscapeDataString(user._id);
            var json = JsonSerializer.Serialize(user, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var putUrl = $"{_couchBase}/{_dbName}/{encodedId}";

            var resp = await _client.PutAsync(putUrl, content);
            return resp.IsSuccessStatusCode;
        }

        public async Task<string?> LoginUserAsync(string email, string password)
        {
            var user = await GetUserByEmailAsync(email);
            if (user == null) return null;

            var ok = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            if (!ok) return null;

            return GenerateJwtToken(user);
        }

        private string GenerateJwtToken(UserDoc user)
        {
            if (string.IsNullOrEmpty(_jwtOptions.Key))
                throw new InvalidOperationException("JWT Key není nastavený.");

            var keyBytes = Encoding.UTF8.GetBytes(_jwtOptions.Key);
            var key = new SymmetricSecurityKey(keyBytes);
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
            };

            var expireMinutes = _jwtOptions.ExpireMinutes > 0 ? _jwtOptions.ExpireMinutes : 60;

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expireMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // ---------------------------
        // Widgety (Privátní)
        // ---------------------------
        public async Task<List<UserWidgetState>> GetUserWidgetsAsync(string email)
        {
            var user = await GetUserByEmailAsync(email);
            return user?.OpenWidgets ?? new List<UserWidgetState>();
        }

        public async Task<bool> SaveUserWidgetsAsync(string email, List<UserWidgetState> widgets)
        {
            if (widgets == null) widgets = new List<UserWidgetState>();

            async Task<bool> PutWithRevAsync(UserDoc userDoc)
            {
                var encodedId = Uri.EscapeDataString(userDoc._id);
                var url = $"{_couchBase}/{_dbName}/{encodedId}";
                var json = JsonSerializer.Serialize(userDoc, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PutAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    if (doc.RootElement.TryGetProperty("rev", out var revEl))
                        userDoc._rev = revEl.GetString();
                    return true;
                }
                return false;
            }

            var user = await GetUserByEmailAsync(email);
            if (user == null) return false;

            user.OpenWidgets = widgets;

            if (await PutWithRevAsync(user)) return true;

            // Retry při konfliktu
            var freshUser = await GetUserByEmailAsync(email);
            if (freshUser == null) return false;
            freshUser.OpenWidgets = widgets;
            return await PutWithRevAsync(freshUser);
        }

        public async Task TestSaveUserWidgetsAsync()
        {
            string testEmail = "vojtech.zmolik@tul.cz";
            var widgets = new List<UserWidgetState>
            {
                new UserWidgetState { Name = "ForecastWeather", Location = "Prague" },
                new UserWidgetState { Name = "NewsFeed", Location = "Global" }
            };
            await SaveUserWidgetsAsync(testEmail, widgets);
        }

        // ==========================================
        // NOVÉ METODY PRO VEŘEJNÉ WIDGETY
        // (Toto vám chybělo a způsobovalo Error)
        // ==========================================

        public async Task CreateIndexesAsync()
        {
            _logger.LogInformation("[CouchDB] Creating/Verifying indexes...");

            // ZMĚNA NÁZVU NA 'v2' DONUTÍ COUCHDB PŘEGENEROVAT INDEX
            var indexPayloadDate = new
            {
                index = new { fields = new[] { "Type", "CreatedAt" } },
                name = "idx_public_widgets_date_v2", // <--- Změna názvu
                type = "json",
                ddoc = "idx_widgets_date_v2"
            };

            var indexPayloadLikes = new
            {
                index = new { fields = new[] { "Type", "LikesCount" } },
                name = "idx_type_likes_v2", // <--- Změna názvu
                type = "json",
                ddoc = "idx_widgets_likes_v2"
            };

            // 1. Index Datum
            var resp1 = await _client.PostAsync($"{_couchBase}/{_dbName}/_index",
                new StringContent(JsonSerializer.Serialize(indexPayloadDate, _jsonOptions), Encoding.UTF8, "application/json"));

            if (!resp1.IsSuccessStatusCode)
                _logger.LogError($"[CouchDB] Date Index Error: {await resp1.Content.ReadAsStringAsync()}");
            else
                _logger.LogInformation("[CouchDB] Date Index (v2) created.");

            // 2. Index Likes
            var resp2 = await _client.PostAsync($"{_couchBase}/{_dbName}/_index",
                new StringContent(JsonSerializer.Serialize(indexPayloadLikes, _jsonOptions), Encoding.UTF8, "application/json"));

            if (!resp2.IsSuccessStatusCode)
                _logger.LogError($"[CouchDB] Likes Index Error: {await resp2.Content.ReadAsStringAsync()}");
            else
                _logger.LogInformation("[CouchDB] Likes Index (v2) created.");
        }

        public async Task<bool> PublishWidgetAsync(UserDoc author, UserWidgetState widgetData, string publicName)
        {
            var publicWidget = new PublicWidgetDoc
            {
                Id = Guid.NewGuid().ToString(), // Vygenerujeme ID ručně
                Type = "public_widget",
                WidgetType = widgetData.Name,
                PublicName = publicName,
                AuthorEmail = author.Email,
                AuthorName = author.Name,
                WidgetData = widgetData,
                CreatedAt = DateTime.UtcNow,
                LikedBy = new List<string>(),
                LikesCount = 0
            };

            var response = await PostDocumentAsync(publicWidget);
            _logger.LogInformation($"[CouchDB] atempting to publish public widget: {publicWidget.Id} by {author.Email}");
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError($"[CouchDB] Failed to save widget: {response.StatusCode} | {error}");
                return false;
            }

            return true;
        }

        public async Task<List<PublicWidgetDoc>> GetPublicWidgetsAsync(WidgetFilterRequest filter)
        {
            // 1. Selector: Musí obsahovat alespoň Type
            var selector = new Dictionary<string, object>
    {
        { "Type", "public_widget" }
    };

            // Filtry
            if (!string.IsNullOrEmpty(filter.WidgetType))
            {
                selector.Add("WidgetType", filter.WidgetType);
            }

            if (!string.IsNullOrEmpty(filter.Author))
            {
                selector.Add("AuthorName", new { @regex = $"(?i){filter.Author}" });
            }

            if (!string.IsNullOrEmpty(filter.SearchName))
            {
                selector.Add("PublicName", new { @regex = $"(?i){filter.SearchName}" });
            }

            // 2. Řazení a Index-Hint
            var sort = new List<object>();

            if (filter.SortBy == "likes")
            {
                sort.Add(new { LikesCount = "desc" });
                // Trik: Aby CouchDB použila index na LikesCount, musí být pole v selectoru
                if (!selector.ContainsKey("LikesCount"))
                    selector.Add("LikesCount", new { @gt = -1 });
            }
            else
            {
                // Default: Řazení podle data
                sort.Add(new { CreatedAt = "desc" });

                // Trik: Aby CouchDB použila index na CreatedAt, musí být pole v selectoru.
                // @gt = null v CouchDB znamená "všechny hodnoty, které existují" (null je nejmenší hodnota)
                if (!selector.ContainsKey("CreatedAt"))
                    selector.Add("CreatedAt", new { @gt = (string?)null });
            }

            var query = new
            {
                selector = selector,
                //sort = sort,
                limit = filter.PageSize,
                skip = (filter.Page - 1) * filter.PageSize,
                execution_stats = true
            };

            // Debug výpis dotazu
            var jsonQuery = JsonSerializer.Serialize(query, _jsonOptions);
            _logger.LogInformation($"[CouchDB] Sending _find Query: {jsonQuery}");

            var content = new StringContent(jsonQuery, Encoding.UTF8, "application/json");
            var resp = await _client.PostAsync($"{_couchBase}/{_dbName}/_find", content);

            if (!resp.IsSuccessStatusCode)
            {
                var errorTxt = await resp.Content.ReadAsStringAsync();
                _logger.LogError($"[CouchDB] Find error: {resp.StatusCode} - {errorTxt}");
                return new List<PublicWidgetDoc>();
            }

            var result = await resp.Content.ReadAsStringAsync();

            // Debug výpis odpovědi (zkrácený)
            _logger.LogInformation($"[CouchDB] Response length: {result.Length}");

            using var doc = JsonDocument.Parse(result);

            var list = new List<PublicWidgetDoc>();
            if (doc.RootElement.TryGetProperty("docs", out var docs))
            {
                foreach (var d in docs.EnumerateArray())
                {
                    try
                    {
                        var item = JsonSerializer.Deserialize<PublicWidgetDoc>(d.GetRawText(), _jsonOptions);
                        if (item != null) list.Add(item);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"[CouchDB] Failed to deserialize item: {ex.Message}");
                    }
                }
            }
            return list;
        }

        public async Task<List<PublicWidgetDoc>> GetLikedWidgetsAsync(string userEmail)
        {
            var query = new
            {
                selector = new
                {
                    Type = "public_widget",
                    LikedBy = new { @elemMatch = new { @eq = userEmail } } // Opravený selector pro pole
                },
                sort = new[] { new { CreatedAt = "desc" } },
                // Trik pro index:
                selector_hint = new { CreatedAt = new { @gt = 0 } }
            };

            // Poznámka: Mango queries s poli jsou v CouchDB někdy ošemetné. 
            // Pokud by to nefungovalo, můžeme filtrovat v paměti (viz níže).

            // Alternativa (In-Memory Filter) - spolehlivější pokud zlobí indexy
            var selectorSimple = new Dictionary<string, object> { { "Type", "public_widget" } };
            // Získáme vše a vyfiltrujeme v C# (pro malé množství dat ok)

            var content = new StringContent(JsonSerializer.Serialize(new { selector = selectorSimple }, _jsonOptions), Encoding.UTF8, "application/json");
            var resp = await _client.PostAsync($"{_couchBase}/{_dbName}/_find", content);

            if (!resp.IsSuccessStatusCode) return new List<PublicWidgetDoc>();

            var list = new List<PublicWidgetDoc>();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("docs", out var docs))
            {
                foreach (var d in docs.EnumerateArray())
                {
                    var item = JsonSerializer.Deserialize<PublicWidgetDoc>(d.GetRawText(), _jsonOptions);
                    if (item != null && item.LikedBy != null && item.LikedBy.Contains(userEmail))
                    {
                        list.Add(item);
                    }
                }
            }
            return list;
        }

        public async Task<bool> ToggleLikeAsync(string widgetId, string userEmail)
        {
            var resp = await GetDocumentAsync(widgetId);
            if (!resp.IsSuccessStatusCode) return false;

            var widget = JsonSerializer.Deserialize<PublicWidgetDoc>(await resp.Content.ReadAsStringAsync(), _jsonOptions);
            if (widget == null) return false;

            if (widget.AuthorEmail == userEmail) return false; // Autor nemůže lajkovat sám sebe

            if (widget.LikedBy == null) widget.LikedBy = new List<string>();

            if (widget.LikedBy.Contains(userEmail))
            {
                widget.LikedBy.Remove(userEmail);
                widget.LikesCount = Math.Max(0, widget.LikesCount - 1);
            }
            else
            {
                widget.LikedBy.Add(userEmail);
                widget.LikesCount++;
            }

            var putUrl = $"{_couchBase}/{_dbName}/{widget.Id}";
            var putContent = new StringContent(JsonSerializer.Serialize(widget, _jsonOptions), Encoding.UTF8, "application/json");
            var putResp = await _client.PutAsync(putUrl, putContent);

            return putResp.IsSuccessStatusCode;
        }
    }
}