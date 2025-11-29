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
            _logger.LogInformation($"[CouchDB] GET user by email: {email}, URL: {url}");

            try
            {
                var resp = await _client.GetAsync(url);
                var json = await resp.Content.ReadAsStringAsync();

                _logger.LogInformation($"[CouchDB] Response status: {resp.StatusCode}");
                _logger.LogInformation($"[CouchDB] Response body: {json}");

                if (!resp.IsSuccessStatusCode)
                    return null;

                var user = JsonSerializer.Deserialize<UserDoc>(json, _jsonOptions);
                _logger.LogInformation($"[CouchDB] User document found: {user != null}");
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[CouchDB] Chyba při GET user: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> RegisterUserAsync(string name, string email, string password)
        {
            _logger.LogInformation($"[Auth] Registruji uživatele: {email}");

            var existing = await GetUserByEmailAsync(email);
            if (existing != null)
            {
                _logger.LogInformation($"[Auth] Uživatel {email} již existuje.");
                return false;
            }

            var hash = BCrypt.Net.BCrypt.HashPassword(password);

            var user = new UserDoc
            {
                _id = email,
                Type = "user",
                Name = name,
                Email = email,
                PasswordHash = hash,
                OpenWidgets = new List<UserWidgetState>()   // <-- pouze tohle
            };

            var encodedId = Uri.EscapeDataString(user._id);
            var json = JsonSerializer.Serialize(user, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var putUrl = $"{_couchBase}/{_dbName}/{encodedId}";

            var resp = await _client.PutAsync(putUrl, content);
            var result = await resp.Content.ReadAsStringAsync();
            _logger.LogInformation($"[CouchDB] PUT result: {resp.StatusCode} | {result}");

            if (!resp.IsSuccessStatusCode)
                return false;

            using var doc = JsonDocument.Parse(result);
            if (doc.RootElement.TryGetProperty("rev", out var rev))
                user._rev = rev.GetString();

            return true;
        }

        public async Task<string?> LoginUserAsync(string email, string password)
        {
            var user = await GetUserByEmailAsync(email);
            if (user == null)
            {
                _logger.LogInformation($"[Auth] Login failed – user {email} not found.");
                return null;
            }

            var ok = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            if (!ok)
            {
                _logger.LogInformation($"[Auth] Login failed – invalid password for {email}.");
                return null;
            }

            _logger.LogInformation($"[Auth] Login success for {email}");
            return GenerateJwtToken(user);
        }

        private string GenerateJwtToken(UserDoc user)
        {
            if (string.IsNullOrEmpty(_jwtOptions.Key))
                throw new InvalidOperationException("JWT Key není nastavený v .env nebo appsettings.json");

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
        // Widgety – OPRAVENÉ
        // ---------------------------
        public async Task<List<UserWidgetState>> GetUserWidgetsAsync(string email)
        {
            var user = await GetUserByEmailAsync(email);
            if (user == null)
                return new List<UserWidgetState>();

            return user.OpenWidgets ?? new List<UserWidgetState>();
        }

        public async Task<bool> SaveUserWidgetsAsync(string email, List<UserWidgetState> widgets)
        {
            if (widgets == null)
                widgets = new List<UserWidgetState>();

            async Task<bool> PutWithRevAsync(UserDoc userDoc)
            {
                var encodedId = Uri.EscapeDataString(userDoc._id);
                var url = $"{_couchBase}/{_dbName}/{encodedId}";

                var json = JsonSerializer.Serialize(userDoc, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PutAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                _logger.LogInformation($"[CouchDB] PUT result: {response.StatusCode} | {responseBody}");

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("rev", out var revEl))
                        userDoc._rev = revEl.GetString();

                    return true;
                }

                return false;
            }

            // 1) Najdi usera
            var user = await GetUserByEmailAsync(email);
            if (user == null)
                return false;

            // 2) Ulož pouze OpenWidgets
            user.OpenWidgets = widgets;

            // 3) Pokus o save
            if (await PutWithRevAsync(user))
                return true;

            // 4) Pokud konflikt, načti čerstvou revizi a opakuj
            _logger.LogInformation("[CouchDB] Conflict detected – retrying with fresh _rev...");
            var freshUser = await GetUserByEmailAsync(email);
            if (freshUser == null)
                return false;

            freshUser.OpenWidgets = widgets;

            return await PutWithRevAsync(freshUser);
        }

        // Testovací metoda nechána beze změn, jen používá nové uložení
        public async Task TestSaveUserWidgetsAsync()
        {
            string testEmail = "vojtech.zmolik@tul.cz";

            var widgets = new List<UserWidgetState>
            {
                new UserWidgetState { Name = "ForecastWeather", Location = "Prague" },
                new UserWidgetState { Name = "NewsFeed", Location = "Global" }
            };

            bool saved = await SaveUserWidgetsAsync(testEmail, widgets);
            _logger.LogInformation($"[Test] SaveUserWidgetsAsync result: {saved}");

            if (!saved)
            {
                _logger.LogInformation("[Test] Ukládání widgetů selhalo!");
                return;
            }

            var loadedWidgets = await GetUserWidgetsAsync(testEmail);
            _logger.LogInformation($"[Test] Načteno {loadedWidgets.Count} widgetů");

            foreach (var w in loadedWidgets)
                _logger.LogInformation($"[Test] Widget: Name={w.Name}, Location={w.Location}");

            bool allMatch = widgets.All(w => loadedWidgets.Any(lw => lw.Name == w.Name && lw.Location == w.Location));
            _logger.LogInformation($"[Test] Všechny widgety uloženy správně: {allMatch}");
        }

        // ---------------------------
        // Veřejné Widgety (Public API)
        // ---------------------------

        public async Task CreateIndexesAsync()
        {
            _logger.LogInformation("[CouchDB] Creating indexes...");

            // 1. Index pro řazení podle data (Type + CreatedAt)
            // Důležité: 'ddoc' (design document) zajišťuje, že se indexy nepoperou
            var indexPayloadDate = new
            {
                index = new { fields = new[] { "Type", "CreatedAt" } },
                name = "idx_type_createdat",
                type = "json",
                ddoc = "idx_widgets_date"
            };

            // 2. Index pro řazení podle likes (Type + LikesCount)
            var indexPayloadLikes = new
            {
                index = new { fields = new[] { "Type", "LikesCount" } },
                name = "idx_type_likes",
                type = "json",
                ddoc = "idx_widgets_likes"
            };

            // Odeslání indexu 1
            var resp1 = await _client.PostAsync($"{_couchBase}/{_dbName}/_index",
                new StringContent(JsonSerializer.Serialize(indexPayloadDate, _jsonOptions), Encoding.UTF8, "application/json"));

            if (!resp1.IsSuccessStatusCode)
                _logger.LogError($"[CouchDB] Failed to create Date index: {await resp1.Content.ReadAsStringAsync()}");

            // Odeslání indexu 2
            var resp2 = await _client.PostAsync($"{_couchBase}/{_dbName}/_index",
                new StringContent(JsonSerializer.Serialize(indexPayloadLikes, _jsonOptions), Encoding.UTF8, "application/json"));

            if (!resp2.IsSuccessStatusCode)
                _logger.LogError($"[CouchDB] Failed to create Likes index: {await resp2.Content.ReadAsStringAsync()}");
        }

        public async Task<bool> PublishWidgetAsync(UserDoc author, UserWidgetState widgetData, string publicName)
        {
            var publicWidget = new PublicWidgetDoc
            {
                // CouchDB potřebuje ID. Pokud ho nevygenerujete, udělá to sama, 
                // ale je lepší mít kontrolu (Guid).
                Id = Guid.NewGuid().ToString(),

                Type = "public_widget", // KLÍČOVÉ PRO FILTR
                WidgetType = widgetData.Name,
                PublicName = publicName,
                AuthorEmail = author.Email,
                AuthorName = author.Name,
                WidgetData = widgetData,
                CreatedAt = DateTime.UtcNow,
                LikedBy = new List<string>(),
                LikesCount = 0
            };

            // POZOR: Serializujeme explicitně, aby se uložilo "_id" a ne "Id" pokud používáte atributy
            var response = await PostDocumentAsync(publicWidget);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogError($"[CouchDB] Failed to publish widget: {err}");
                return false;
            }

            return true;
        }

        public async Task<List<PublicWidgetDoc>> GetPublicWidgetsAsync(WidgetFilterRequest filter)
        {
            // 1. Selector: Musí obsahovat "Type": "public_widget"
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

            // 2. Řazení
            var sort = new List<object>();

            // DŮLEŽITÉ: CouchDB vyžaduje, aby pole, podle kterého řadíte, 
            // bylo obsaženo v indexu A ideálně i v selectoru (jako existence check).
            if (filter.SortBy == "likes")
            {
                sort.Add(new { LikesCount = "desc" });
                // Trik pro Mango Query: Říkáme, že LikesCount musí existovat (být větší než null)
                // To pomůže CouchDB vybrat správný index.
                if (!selector.ContainsKey("LikesCount")) selector.Add("LikesCount", new { @gt = -1 });
            }
            else
            {
                sort.Add(new { CreatedAt = "desc" });
                // Trik pro Mango Query:
                if (!selector.ContainsKey("CreatedAt")) selector.Add("CreatedAt", new { @gt = 0 });
            }

            var query = new
            {
                selector = selector,
                sort = sort,
                limit = filter.PageSize,
                skip = (filter.Page - 1) * filter.PageSize,
                execution_stats = true
            };

            var jsonQuery = JsonSerializer.Serialize(query, _jsonOptions);
            _logger.LogInformation($"[CouchDB] Sending Query: {jsonQuery}"); // DEBUG VÝPIS

            var content = new StringContent(jsonQuery, Encoding.UTF8, "application/json");
            var resp = await _client.PostAsync($"{_couchBase}/{_dbName}/_find", content);

            // 3. Zpracování chyby
            if (!resp.IsSuccessStatusCode)
            {
                var errorBody = await resp.Content.ReadAsStringAsync();
                _logger.LogError($"[CouchDB] _find query failed: {resp.StatusCode} | {errorBody}");
                // Vraťte prázdný list, ale v konzoli uvidíte chybu
                return new List<PublicWidgetDoc>();
            }

            var result = await resp.Content.ReadAsStringAsync();
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
                        _logger.LogError($"[CouchDB] Failed to deserialize item: {ex.Message}");
                    }
                }
            }

            _logger.LogInformation($"[CouchDB] Found {list.Count} widgets.");
            return list;
        }
    }
}
