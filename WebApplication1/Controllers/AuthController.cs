using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using WebApplication1.Models;
using WebApplication1.Service;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly CouchDbService _couch;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthController> _logger;

        public AuthController(CouchDbService couch, IConfiguration config, ILogger<AuthController> logger)
        {
            _couch = couch;
            _config = config;
            _logger = logger;
        }

        // --------------------
        // DTOs
        // --------------------
        public record RegisterDto(string Name, string Email, string Password);
        public record LoginDto(string Email, string Password);
        public record GoogleAuthDto(string Code);

        // --------------------
        // Registrace
        // --------------------
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            _logger.LogInformation("[AuthControllerRegister] započnuta registrace");
            var ok = await _couch.RegisterUserAsync(dto.Name, dto.Email, dto.Password);
            if (!ok)
            {
                _logger.LogWarning("[AuthControllerRegister] registrace selhala - email již používán"); ;
                return Conflict(new { message = "Email already in use" });
            }

            _logger.LogInformation("[AuthControllerRegister] registrace úspěšná");
            return Ok(new { message = "Registered" });
        }

        // --------------------
        // Login
        // --------------------
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            _logger.LogInformation("[AuthControllerLogin] započat login");
            var token = await _couch.LoginUserAsync(dto.Email, dto.Password);
            if (token == null)
            {
                _logger.LogWarning("[AuthControllerLogin] login selhal - neplatné údaje");
                return Unauthorized(new { message = "Invalid credentials" });
            }

            _logger.LogInformation("[AuthControllerLogin] login úspěšný");
            return Ok(new { token });
        }

        // --------------------
        // Google OAuth2 (code -> JWT)
        // --------------------

        [HttpPost("google/exchange")]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleExchange([FromBody] GoogleAuthDto dto)
        {
            _logger.LogInformation("[GoogleOAuth] započata výměna tokenu");
            if (string.IsNullOrWhiteSpace(dto.Code))
            {
                _logger.LogWarning("[GoogleOAuth] chybí autorizační kód");
                return BadRequest(new { message = "Missing authorization code" });
            }

            // načti Google konfiguraci z .env
            var clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENTID");
            var clientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENTSECRET");
            var redirectUri = Environment.GetEnvironmentVariable("GOOGLE_REDIRECTURI");

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(redirectUri))
            {
                _logger.LogError("[GoogleOAuth] chybí Google OAuth environmentální proměnné");
                return StatusCode(500, new { message = "Google OAuth environment variables not set" });
            }

            using var http = new HttpClient();

            // 1️⃣ Výměna code → tokeny
            var values = new Dictionary<string, string>
            {
                ["code"] = dto.Code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            };

            var tokenResp = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(values));
            var tokenText = await tokenResp.Content.ReadAsStringAsync();

            if (!tokenResp.IsSuccessStatusCode)
            {
                _logger.LogError($"[GoogleOAuth] výměna tokenu selhala: {tokenText}");
                return BadRequest(new { message = "Google token exchange failed", details = tokenText });
            }

            using var tokenDoc = JsonDocument.Parse(tokenText);
            if (!tokenDoc.RootElement.TryGetProperty("id_token", out var idTokenEl))
            {
                _logger.LogError("[GoogleOAuth] chybí id_token v odpovědi Google");
                return BadRequest(new { message = "Missing id_token in Google response" });
            }

            var idToken = idTokenEl.GetString();
            if (string.IsNullOrEmpty(idToken))
            {
                _logger.LogError("[GoogleOAuth] neplatný id_token v odpovědi Google");
                return BadRequest(new { message = "Invalid id_token in Google response" });
            }

            // 2️⃣ Ověření id_token
            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[GoogleOAuth] ověření tokenu selhalo: {ex.Message}");
                return BadRequest(new { message = "Invalid Google token", error = ex.Message });
            }

            if (!payload.EmailVerified)
            {
                _logger.LogWarning("[GoogleOAuth] email Google účtu není ověřen");
                return BadRequest(new { message = "Google account email not verified" });
            }

            var email = payload.Email.ToLowerInvariant();
            var name = payload.Name ?? email.Split('@')[0];

            // 3️⃣ Najdi nebo vytvoř uživatele v CouchDB
            var user = await _couch.GetUserByEmailAsync(email);
            if (user == null)
            {
                _logger.LogInformation($"[GoogleOAuth] 🆕 Vytváří se nový uživatel {email}");
                var randomPass = Guid.NewGuid().ToString(); // placeholder, nikdy se nepoužije
                var ok = await _couch.RegisterUserAsync(name, email, randomPass);
                if (!ok)
                {
                    _logger.LogError("[GoogleOAuth] selhalo vytvoření nového uživatele");
                    return StatusCode(500, new { message = "Failed to create new user" });
                }

                user = await _couch.GetUserByEmailAsync(email);
            }
            else
            {
                _logger.LogInformation($"[GoogleOAuth] ✅ Existující uživatel {email}");
            }

            if (user == null)
                return StatusCode(500, new { message = "User record not found after creation" });

            // 4️⃣ Vygeneruj vlastní JWT pro aplikaci
            var jwt = _couch.GenerateJwtForExistingUser(user);

            return Ok(new
            {
                token = jwt,
                email = email,
                name = name
            });
        }

        // --------------------
        // Widgety
        // --------------------
        [HttpGet("widgets")]
        [Authorize]
        public async Task<IActionResult> GetWidgets()
        {
            // Použij standardní claim typ (funguje u všech tokenů)
            var email = User.FindFirst(ClaimTypes.Email)?.Value
                     ?? User.FindFirst("email")?.Value;

            if (string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("[AuthControllerWidgets] chybí email claim v JWT");
                foreach (var c in User.Claims)
                    Console.WriteLine($"  - {c.Type} = {c.Value}");

                return Unauthorized("Email claim missing");
            }

            _logger.LogInformation($"[AuthControllerWidgets] ✅ Authenticated email: {email}");

            var widgets = await _couch.GetUserWidgetsAsync(email);
            return Ok(widgets);
        }

        [HttpPost("widgets")]
        [Authorize]
        public async Task<IActionResult> SaveWidgets([FromBody] List<UserWidgetState> widgets)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email))
                return Unauthorized();

            var ok = await _couch.SaveUserWidgetsAsync(email, widgets);
            //await _couch.TestSaveUserWidgetsAsync();
            if (!ok)
            {
                //if (true)
                _logger.LogError("[AuthControllerSaveWidgets] selhalo ukládání widgetů");
                return BadRequest(new { message = "Could not save widgets" });
            }

            _logger.LogInformation("[AuthControllerSaveWidgets] widgety úspěšně uloženy");
            return Ok(new { message = "Widgets saved" });
        }
    }
}