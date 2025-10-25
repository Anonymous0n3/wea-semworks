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
        private readonly string _dbName = "helloworld";
        private readonly string _couchBase;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly JwtOptions _jwtOptions;

        public CouchDbService(HttpClient client, JwtOptions jwtOptions, IConfiguration config)
        {
            _client = client;
            _jwtOptions = jwtOptions;
            _couchBase = config["COUCHDB_URL"] ?? "http://couchdb:5984";

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
            Console.WriteLine($"[CouchDB] GET user by email: {email}, URL: {url}");

            try
            {
                var resp = await _client.GetAsync(url);
                var json = await resp.Content.ReadAsStringAsync();

                Console.WriteLine($"[CouchDB] Response status: {resp.StatusCode}");
                Console.WriteLine($"[CouchDB] Response body: {json}");

                if (!resp.IsSuccessStatusCode)
                    return null;

                var user = JsonSerializer.Deserialize<UserDoc>(json, _jsonOptions);
                Console.WriteLine($"[CouchDB] Uživatelský dokument nalezen: {user != null}");
                return user;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CouchDB] Chyba při GET user: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> RegisterUserAsync(string name, string email, string password)
        {
            Console.WriteLine($"[Auth] Registruji uživatele: {email}");

            var existing = await GetUserByEmailAsync(email);
            if (existing != null)
            {
                Console.WriteLine($"[Auth] Uživatel {email} již existuje.");
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
                DashboardStateJson = "[]"
            };

            var encodedId = Uri.EscapeDataString(user._id);
            var json = JsonSerializer.Serialize(user, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var putUrl = $"{_couchBase}/{_dbName}/{encodedId}";

            var resp = await _client.PutAsync(putUrl, content);
            var result = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"[CouchDB] PUT result: {resp.StatusCode} | {result}");

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
                Console.WriteLine($"[Auth] Login failed – user {email} not found.");
                return null;
            }

            var ok = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            if (!ok)
            {
                Console.WriteLine($"[Auth] Login failed – invalid password for {email}.");
                return null;
            }

            Console.WriteLine($"[Auth] Login success for {email}");
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
        // Widgety
        // ---------------------------
        public async Task<List<UserWidgetState>> GetUserWidgetsAsync(string email)
        {
            var user = await GetUserByEmailAsync(email);
            if (user == null) return new List<UserWidgetState>();

            try
            {
                if (string.IsNullOrWhiteSpace(user.DashboardStateJson))
                    return new List<UserWidgetState>();

                var json = user.DashboardStateJson.Trim();

                // pokud CouchDB vrátila JSON jako string "[{...}]", zkusíme nejdřív rozbalit
                if (json.StartsWith("\"") && json.EndsWith("\""))
                {
                    json = JsonSerializer.Deserialize<string>(json) ?? "[]";
                }

                return JsonSerializer.Deserialize<List<UserWidgetState>>(json, _jsonOptions)
                       ?? new List<UserWidgetState>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Widgets] Chyba při deserializaci DashboardStateJson: {ex.Message}");
                return new List<UserWidgetState>();
            }
        }

        public async Task<bool> SaveUserWidgetsAsync(string email, List<UserWidgetState> widgets)
        {
            if (widgets == null)
                widgets = new List<UserWidgetState>();

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            async Task<bool> PutWithRevAsync(UserDoc userDoc)
            {
                var encodedId = Uri.EscapeDataString(userDoc._id);
                var url = $"{_couchBase}/{_dbName}/{encodedId}";

                var json = JsonSerializer.Serialize(userDoc, jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PutAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"[CouchDB] PUT result: {response.StatusCode} | {responseBody}");

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("rev", out var revEl))
                        userDoc._rev = revEl.GetString();
                    return true;
                }

                return false;
            }

            // 1️⃣ Načti aktuální dokument
            var user = await GetUserByEmailAsync(email);
            if (user == null)
            {
                Console.WriteLine($"[CouchDB] User {email} not found.");
                return false;
            }

            // 2️⃣ Aktualizuj data widgetů
            user.DashboardStateJson = JsonSerializer.Serialize(widgets, jsonOptions);
            user.OpenWidgets = widgets;

            // 3️⃣ Zkus první PUT
            var success = await PutWithRevAsync(user);
            if (success)
            {
                Console.WriteLine("[CouchDB] Widgety úspěšně uloženy.");
                return true;
            }

            // 4️⃣ Pokud konflikt – načti nejnovější rev a zkus znovu
            Console.WriteLine("[CouchDB] Conflict detected – retrying with fresh _rev...");
            var freshUser = await GetUserByEmailAsync(email);
            if (freshUser == null)
            {
                Console.WriteLine("[CouchDB] Retry failed – user not found.");
                return false;
            }

            freshUser.DashboardStateJson = JsonSerializer.Serialize(widgets, jsonOptions);
            freshUser.OpenWidgets = widgets;

            var retrySuccess = await PutWithRevAsync(freshUser);
            if (retrySuccess)
            {
                Console.WriteLine("[CouchDB] Retry succeeded – widgety uloženy.");
                return true;
            }

            Console.WriteLine("[CouchDB] Retry failed – document still in conflict.");
            return false;
        }

        public async Task TestSaveUserWidgetsAsync()
        {
            string testEmail = "vojtech.zmolik@tul.cz";

            // 1️⃣ Připrav testovací widgety
            var widgets = new List<UserWidgetState>
    {
        new UserWidgetState { Name = "ForecastWeather", Location = "Prague" },
        new UserWidgetState { Name = "NewsFeed", Location = "Global" }
    };

            // 2️⃣ Ulož widgety
            bool saved = await SaveUserWidgetsAsync(testEmail, widgets);
            Console.WriteLine($"[Test] SaveUserWidgetsAsync result: {saved}");

            if (!saved)
            {
                Console.WriteLine("[Test] Ukládání widgetů selhalo!");
                return;
            }

            // 3️⃣ Načti widgety z CouchDB
            var loadedWidgets = await GetUserWidgetsAsync(testEmail);
            Console.WriteLine($"[Test] Načteno {loadedWidgets.Count} widgetů");

            foreach (var w in loadedWidgets)
            {
                Console.WriteLine($"[Test] Widget: Name={w.Name}, Location={w.Location}");
            }

            // 4️⃣ Kontrola, jestli jsou všechny widgety uložené správně
            bool allMatch = widgets.All(w => loadedWidgets.Any(lw => lw.Name == w.Name && lw.Location == w.Location));
            Console.WriteLine($"[Test] Všechny widgety uloženy správně: {allMatch}");
        }


    }
}
