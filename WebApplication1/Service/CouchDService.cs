using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            _couchBase = config["COUCHDB_URL"] ?? "http://couchdb:5984";
            _logger = logger;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = null // Zachová PascalCase (Type, CreatedAt...)
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

        // ---------------------------
        // Inicializace DB a Indexů
        // ---------------------------
        public async Task EnsureDbExistsAsync()
        {
            var dbUri = $"{_couchBase}/{_dbName}";
            var head = await _client.GetAsync(dbUri);

            if (head.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await _client.PutAsync(dbUri, null);
            }
        }

        public async Task CreateIndexesAsync()
        {
            // Index pro Type + CreatedAt (pro řazení podle data)
            var indexPayload = new
            {
                index = new { fields = new[] { "Type", "CreatedAt" } },
                name = "sort_by_date",
                type = "json"
            };

            // Index pro Type + LikesCount (pro řazení podle lajků)
            var indexPayloadLikes = new
            {
                index = new { fields = new[] { "Type", "LikesCount" } },
                name = "sort_by_likes",
                type = "json"
            };

            await _client.PostAsync($"{_couchBase}/{_dbName}/_index",
                new StringContent(JsonSerializer.Serialize(indexPayload, _jsonOptions), Encoding.UTF8, "application/json"));

            await _client.PostAsync($"{_couchBase}/{_dbName}/_index",
                new StringContent(JsonSerializer.Serialize(indexPayloadLikes, _jsonOptions), Encoding.UTF8, "application/json"));
        }

        // ---------------------------
        // Základní CRUD operace
        // ---------------------------
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
            if (!resp.IsSuccessStatusCode) return new List<HelloDoc>();

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var list = new List<HelloDoc>();
            if (doc.RootElement.TryGetProperty("rows", out var rows))
            {
                foreach (var row in rows.EnumerateArray())
                {
                    if (row.TryGetProperty("doc", out var d))
                    {
                        var item = JsonSerializer.Deserialize<HelloDoc>(d.GetRawText(), _jsonOptions);
                        if (item != null) list.Add(item);
                    }
                }
            }
            return list;
        }

        // ---------------------------
        // Uživatelé a Auth
        // ---------------------------
        public async Task<UserDoc?> GetUserByEmailAsync(string email)
        {
            var encodedId = Uri.EscapeDataString(email);
            var resp = await _client.GetAsync($"{_couchBase}/{_dbName}/{encodedId}");
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<UserDoc>(json, _jsonOptions);
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
            var resp = await _client.PutAsync($"{_couchBase}/{_dbName}/{encodedId}", content);

            return resp.IsSuccessStatusCode;
        }

        public async Task<string?> LoginUserAsync(string email, string password)
        {
            var user = await GetUserByEmailAsync(email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return null;
            return GenerateJwtToken(user);
        }

        private string GenerateJwtToken(UserDoc user)
        {
            var keyBytes = Encoding.UTF8.GetBytes(_jwtOptions.Key);
            var key = new SymmetricSecurityKey(keyBytes);
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
            };

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtOptions.ExpireMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // ---------------------------
        // Widgety uživatele (Workspace)
        // ---------------------------
        public async Task<List<UserWidgetState>> GetUserWidgetsAsync(string email)
        {
            var user = await GetUserByEmailAsync(email);
            return user?.OpenWidgets ?? new List<UserWidgetState>();
        }

        public async Task<bool> SaveUserWidgetsAsync(string email, List<UserWidgetState> widgets)
        {
            var user = await GetUserByEmailAsync(email);
            if (user == null) return false;

            user.OpenWidgets = widgets ?? new List<UserWidgetState>();

            var encodedId = Uri.EscapeDataString(user._id);
            var json = JsonSerializer.Serialize(user, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _client.PutAsync($"{_couchBase}/{_dbName}/{encodedId}", content);

            return resp.IsSuccessStatusCode;
        }

        // ---------------------------
        // Veřejné Widgety (Public API)
        // ---------------------------
        public async Task<bool> PublishWidgetAsync(UserDoc author, UserWidgetState widgetData, string publicName)
        {
            var publicWidget = new PublicWidgetDoc
            {
                WidgetType = widgetData.Name,
                PublicName = publicName,
                AuthorEmail = author.Email,
                AuthorName = author.Name,
                WidgetData = widgetData,
                CreatedAt = DateTime.UtcNow,
                Type = "public_widget", // Důležité pro selektor
                LikesCount = 0,
                LikedBy = new List<string>()
            };

            var result = await PostDocumentAsync(publicWidget);
            return result.IsSuccessStatusCode;
        }

        public async Task<List<PublicWidgetDoc>> GetPublicWidgetsAsync(WidgetFilterRequest filter)
        {
            var selector = new Dictionary<string, object>
            {
                { "Type", "public_widget" }
            };

            // Filtr podle typu widgetu
            if (!string.IsNullOrEmpty(filter.WidgetType))
            {
                selector.Add("WidgetType", filter.WidgetType);
            }

            // Filtr podle autora (Case insensitive regex)
            if (!string.IsNullOrEmpty(filter.Author))
            {
                selector.Add("AuthorName", new { @regex = $"(?i){filter.Author}" });
            }

            // Filtr podle názvu (Case insensitive regex)
            if (!string.IsNullOrEmpty(filter.SearchName))
            {
                selector.Add("PublicName", new { @regex = $"(?i){filter.SearchName}" });
            }

            // Řazení
            var sort = new List<object>();
            if (filter.SortBy == "likes")
                sort.Add(new { LikesCount = "desc" });
            else
                sort.Add(new { CreatedAt = "desc" });

            // 1. Pokus s řazením
            var query = new
            {
                selector = selector,
                sort = sort,
                limit = filter.PageSize,
                skip = (filter.Page - 1) * filter.PageSize,
                execution_stats = true
            };

            var resultList = await ExecuteMangoQueryAsync(query);

            // 2. FALLBACK: Pokud řazení selže (chybí index), zkusíme to bez řazení
            if (resultList == null)
            {
                _logger.LogWarning("[CouchDB] Query with sort failed. Retrying without sort...");
                var queryNoSort = new
                {
                    selector = selector,
                    limit = filter.PageSize,
                    skip = (filter.Page - 1) * filter.PageSize
                };
                resultList = await ExecuteMangoQueryAsync(queryNoSort);
            }

            return resultList ?? new List<PublicWidgetDoc>();
        }

        public async Task<List<PublicWidgetDoc>> GetLikedWidgetsAsync(string userEmail)
        {
            var query = new
            {
                selector = new
                {
                    Type = "public_widget",
                    LikedBy = newDictionary("$elemMatch", new { @eq = userEmail }) // nebo prostě jen pole hodnotu
                },
                // Zjednodušený selector pro pole v CouchDB
                // Často stačí: LikedBy: userEmail
            };

            // CouchDB Mango query pro pole: { "LikedBy": { "$elemMatch": { "$eq": "email" } } }
            // Nebo jednodušeji { "LikedBy": "email" } funguje v mnoha verzích pokud je to pole stringů.

            var simpleQuery = new
            {
                selector = new
                {
                    Type = "public_widget",
                    LikedBy = userEmail
                }
            };

            return await ExecuteMangoQueryAsync(simpleQuery) ?? new List<PublicWidgetDoc>();
        }

        // Pomocná metoda pro vykonání dotazu
        private async Task<List<PublicWidgetDoc>?> ExecuteMangoQueryAsync(object queryObj)
        {
            try
            {
                var jsonQuery = JsonSerializer.Serialize(queryObj, _jsonOptions);
                // Fix pro regex operátor (C# objekt -> $regex)
                jsonQuery = jsonQuery.Replace("\"@regex\"", "\"$regex\"");

                var content = new StringContent(jsonQuery, Encoding.UTF8, "application/json");
                var resp = await _client.PostAsync($"{_couchBase}/{_dbName}/_find", content);

                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    _logger.LogError($"[CouchDB] Find failed: {resp.StatusCode} - {err}");
                    return null;
                }

                var result = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(result);

                var list = new List<PublicWidgetDoc>();
                if (doc.RootElement.TryGetProperty("docs", out var docs))
                {
                    foreach (var d in docs.EnumerateArray())
                    {
                        var item = JsonSerializer.Deserialize<PublicWidgetDoc>(d.GetRawText(), _jsonOptions);
                        if (item != null) list.Add(item);
                    }
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CouchDB] ExecuteMangoQueryAsync exception");
                return null;
            }
        }

        // Helper proDictionary
        private Dictionary<string, object> newDictionary(string k, object v)
        {
            return new Dictionary<string, object> { { k, v } };
        }

        public async Task<bool> ToggleLikeAsync(string widgetId, string userEmail)
        {
            var resp = await GetDocumentAsync(widgetId);
            if (!resp.IsSuccessStatusCode) return false;

            var json = await resp.Content.ReadAsStringAsync();
            var widget = JsonSerializer.Deserialize<PublicWidgetDoc>(json, _jsonOptions);
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