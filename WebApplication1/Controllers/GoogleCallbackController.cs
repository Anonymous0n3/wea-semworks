using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/auth/google")]
    public class GoogleCallbackController : ControllerBase
    {
        private readonly IConfiguration _config;

        public GoogleCallbackController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet("callback")]
        public async Task<IActionResult> GoogleCallback([FromQuery] string code)
        {
            if (string.IsNullOrEmpty(code))
                return BadRequest("Missing Google auth code");

            using var http = new HttpClient();
            var exchangeUrl = $"{Request.Scheme}://{Request.Host}/api/auth/google/exchange";

            var res = await http.PostAsync(exchangeUrl, new StringContent(
                JsonSerializer.Serialize(new { code }),
                Encoding.UTF8,
                "application/json"
            ));

            if (!res.IsSuccessStatusCode)
            {
                var msg = await res.Content.ReadAsStringAsync();
                return Content($"Google login failed: {msg}");
            }

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var token = doc.RootElement.GetProperty("token").GetString();

            // ✅ Uloží JWT token do cookie
            Response.Cookies.Append("jwtToken", token, new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });

            // ✅ Přesměruje zpět na Dashboard (root)
            return Redirect($"{Request.Scheme}://{Request.Host}/");
        }
    }
}
