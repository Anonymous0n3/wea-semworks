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

        public async Task<string> GetRawDbDumpAsync()
        {
            var resp = await _client.GetAsync($"{_couchBase}/{_dbName}/_all_docs?include_docs=true");
            return await resp.Content.ReadAsStringAsync();
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
        // VEŘEJNÉ WIDGETY (PUBLIC API) - FIXED
        // ==========================================

        /*public async Task CreateIndexesAsync()
        {
            _logger.LogInformation("[CouchDB] Creating/Verifying indexes...");

            // Pro jistotu vytvoříme jednoduchý index na Type, ten se vždy hodí
            var indexData = new
            {
                index = new { fields = new[] { "Type" } },
                name = "idx_type_simple",
                type = "json"
            };

            await _client.PostAsync($"{_couchBase}/{_dbName}/_index",
                new StringContent(JsonSerializer.Serialize(indexData, _jsonOptions), Encoding.UTF8, "application/json"));
        }*/

        public async Task<bool> PublishWidgetAsync(UserDoc author, UserWidgetState widgetData, string publicName)
        {
            var publicWidget = new PublicWidgetDoc
            {
                Id = Guid.NewGuid().ToString(),
                Type = "public_widget", // DŮLEŽITÉ: Identifikátor typu
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

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError($"[CouchDB] Publish failed: {error}");
                return false;
            }
            return true;
        }

        public async Task CreateIndexesAsync()
        {
            _logger.LogInformation("[CouchDB] Creating/Verifying indexes...");

            // Index 1: Pro základní filtrování podle typu
            var indexDataType = new
            {
                index = new { fields = new[] { "Type" } },
                name = "idx_type_simple",
                type = "json"
            };

            // Index 2: Pro Řazení podle Oblíbenosti (LikesCount)
            // Musí obsahovat "Type" jako první pole, protože to je náš hlavní filtr
            var indexDataLikes = new
            {
                index = new { fields = new[] { "Type", "LikesCount" } },
                name = "idx_type_likes",
                type = "json"
            };

            // Index 3: Pro Řazení podle Data Vytvoření (CreatedAt)
            var indexDataDate = new
            {
                index = new { fields = new[] { "Type", "CreatedAt" } },
                name = "idx_type_date",
                type = "json"
            };

            // Index 4: Pro filtrování PublicName
            var indexDataPublicName = new
            {
                index = new { fields = new[] { "Type", "PublicName" } },
                name = "idx_type_publicname",
                type = "json"
            };

            // Vytvoření všech indexů
            var indexList = new[] { indexDataType, indexDataLikes, indexDataDate, indexDataPublicName };

            foreach (var indexData in indexList)
            {
                var content = new StringContent(JsonSerializer.Serialize(indexData, _jsonOptions), Encoding.UTF8, "application/json");
                var resp = await _client.PostAsync($"{_couchBase}/{_dbName}/_index", content);

                if (!resp.IsSuccessStatusCode)
                {
                    var error = await resp.Content.ReadAsStringAsync();
                    _logger.LogError($"[CouchDB] Failed to create index {indexData.name}: {error}");
                }
            }
        }

        // -------------------------------------------------------------
        // OPRAVA: In-Memory Filtering (Spolehlivější než Mango Sort)
        // -------------------------------------------------------------
        /*public async Task<List<PublicWidgetDoc>> GetPublicWidgetsAsync(WidgetFilterRequest filter)
        {
            // 1. Stáhneme VŠECHNY dokumenty typu 'public_widget' (bez sortu v DB)

            var query = new
            {
                selector = new { Type = "public_widget" },
                limit = 2000
            };

            var content = new StringContent(JsonSerializer.Serialize(query, _jsonOptions), Encoding.UTF8, "application/json");
            var resp = await _client.PostAsync($"{_couchBase}/{_dbName}/_find", content);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError($"[CouchDB] Find failed: {resp.StatusCode}");
                return new List<PublicWidgetDoc>();
            }

            var result = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(result);

            var list = new List<PublicWidgetDoc>();

            // 2. Deserializace
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
                        _logger.LogWarning($"Deserialization error: {ex.Message}");
                    }
                }
            }

            // 3. Filtrování a Řazení v paměti C# (LINQ)
            var queryable = list.AsEnumerable();

            if (!string.IsNullOrEmpty(filter.WidgetType))
                queryable = queryable.Where(w => w.WidgetType == filter.WidgetType);

            if (!string.IsNullOrEmpty(filter.Author))
                queryable = queryable.Where(w => w.AuthorName != null && w.AuthorName.Contains(filter.Author, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(filter.SearchName))
                queryable = queryable.Where(w => w.PublicName != null && w.PublicName.Contains(filter.SearchName, StringComparison.OrdinalIgnoreCase));

            if (filter.SortBy == "likes")
                queryable = queryable.OrderByDescending(w => w.LikesCount);
            else
                queryable = queryable.OrderByDescending(w => w.CreatedAt);

            // 4. Stránkování
            return queryable
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToList();
        }*/

        // -------------------------------------------------------------
        // OPRAVA: Filtrování, řazení a stránkování přímo v DB dotazu (Mango Query)
        // -------------------------------------------------------------
        public async Task<List<PublicWidgetDoc>> GetPublicWidgetsAsync(WidgetFilterRequest filter)
        {
            // 1. Sestavení Mango Query (Filtrování, Řazení a Stránkování v DB)

            // Používáme Dictionary<string, object> pro dynamické sestavení "selector" objektu,
            // abychom se vyhnuli chybám spojeným s neměnnými anonymními typy v C# při dynamickém přidávání polí.
            var selector = new Dictionary<string, object>
    {
        // Pevný filtr: Vždy filtrujeme podle typu dokumentu
        { "Type", "public_widget" }
    };

            // Dynamické přidání filtru podle typu widgetu (přesná shoda)
            if (!string.IsNullOrEmpty(filter.WidgetType))
            {
                selector.Add("WidgetType", filter.WidgetType);
            }

            // Dynamické přidání filtru pro PublicName (textové vyhledávání - $regex pro "obsahuje")
            if (!string.IsNullOrEmpty(filter.SearchName))
            {
                // (?i) zajišťuje case-insensitive (ignoruje velikost písmen)
                selector.Add("PublicName", new Dictionary<string, object>
                {
                    ["$regex"] = $"(?i){Uri.EscapeDataString(filter.SearchName)}"
                });
            }

            // Dynamické přidání filtru pro AuthorName (textové vyhledávání - $regex pro "obsahuje")
            if (!string.IsNullOrEmpty(filter.Author))
            {
                // (?i) zajišťuje case-insensitive (ignoruje velikost písmen)
                selector.Add("AuthorName", new Dictionary<string, object>
                {
                    ["$regex"] = $"(?i){Uri.EscapeDataString(filter.Author)}"
                });
            }

            // 2. Sestavení pole pro řazení (Sort)
            var sortField = filter.SortBy == "likes" ? "LikesCount" : "CreatedAt";
            var sortOrder = "desc";

            // CouchDB očekává pole objektů pro sort, např.: [{"LikesCount": "desc"}]
            var sort = new List<object>
    {
        new Dictionary<string, string> { { sortField, sortOrder } }
    };

            // 3. Kompletní Mango Query objekt pro odeslání do CouchDB
            var query = new
            {
                selector = selector,
                sort = sort,
                // Stránkování pomocí DB parametrů skip/limit
                skip = (filter.Page - 1) * filter.PageSize,
                limit = filter.PageSize
            };

            // Odeslání Mango Query do CouchDB
            var content = new StringContent(JsonSerializer.Serialize(query, _jsonOptions), Encoding.UTF8, "application/json");
            var resp = await _client.PostAsync($"{_couchBase}/{_dbName}/_find", content);

            if (!resp.IsSuccessStatusCode)
            {
                var errorContent = await resp.Content.ReadAsStringAsync();
                _logger.LogError($"[CouchDB] Find failed: {resp.StatusCode}. Error detail: {errorContent}. Query was: {JsonSerializer.Serialize(query, _jsonOptions)}");
                return new List<PublicWidgetDoc>();
            }

            // Zpracování a deserializace odpovědi
            var result = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(result);

            var list = new List<PublicWidgetDoc>();

            if (doc.RootElement.TryGetProperty("docs", out var docs))
            {
                foreach (var d in docs.EnumerateArray())
                {
                    try
                    {
                        // Deserializujeme pouze dokumenty vrácené databází
                        var item = JsonSerializer.Deserialize<PublicWidgetDoc>(d.GetRawText(), _jsonOptions);
                        if (item != null) list.Add(item);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Deserialization error: {ex.Message}");
                    }
                }
            }

            // Vracíme rovnou výsledek, protože byl již filtrován, seřazen a stránkován v DB.
            return list;
        }

        public async Task<List<PublicWidgetDoc>> GetLikedWidgetsAsync(string userEmail)
        {
            // Stejný princip: Stáhneme vše a vyfiltrujeme v paměti
            var query = new { selector = new { Type = "public_widget" } };

            var content = new StringContent(JsonSerializer.Serialize(query, _jsonOptions), Encoding.UTF8, "application/json");
            var resp = await _client.PostAsync($"{_couchBase}/{_dbName}/_find", content);

            if (!resp.IsSuccessStatusCode) return new List<PublicWidgetDoc>();

            var list = new List<PublicWidgetDoc>();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

            if (doc.RootElement.TryGetProperty("docs", out var docs))
            {
                foreach (var d in docs.EnumerateArray())
                {
                    var item = JsonSerializer.Deserialize<PublicWidgetDoc>(d.GetRawText(), _jsonOptions);
                    // Kontrola v paměti
                    if (item != null && item.LikedBy != null && item.LikedBy.Contains(userEmail))
                    {
                        list.Add(item);
                    }
                }
            }
            return list.OrderByDescending(w => w.CreatedAt).ToList();
        }

        public async Task<bool> ToggleLikeAsync(string widgetId, string userEmail)
        {
            var resp = await GetDocumentAsync(widgetId);
            if (!resp.IsSuccessStatusCode) return false;

            var widget = JsonSerializer.Deserialize<PublicWidgetDoc>(await resp.Content.ReadAsStringAsync(), _jsonOptions);
            if (widget == null) return false;

            if (widget.AuthorEmail == userEmail) return false;

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