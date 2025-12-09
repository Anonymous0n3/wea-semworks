using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebApplication1.Models;
using WebApplication1.Service;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PublicWidgetsController : ControllerBase
    {
        private readonly CouchDbService _couchService;
        private readonly ILogger<PublicWidgetsController> _logger;

        public PublicWidgetsController(CouchDbService couchService, ILogger<PublicWidgetsController> logger)
        {
            _couchService = couchService;
            _logger = logger;
        }

        // 1. Seznam veřejných widgetů
        [HttpPost("list")]
        [AllowAnonymous]
        public async Task<IActionResult> GetList([FromBody] WidgetFilterRequest filter)
        {
            try
            {
                var widgets = await _couchService.GetPublicWidgetsAsync(filter);
                return Ok(widgets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chyba při načítání veřejných widgetů");
                return StatusCode(500, "Interní chyba");
            }
        }

        // 2. Publikování
        [HttpPost("publish")]
        [Authorize]
        public async Task<IActionResult> Publish([FromBody] PublishRequest request)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value
                     ?? User.FindFirst("email")?.Value;

            if (string.IsNullOrEmpty(email)) return Unauthorized();

            var user = await _couchService.GetUserByEmailAsync(email);
            if (user == null) return Unauthorized();

            var success = await _couchService.PublishWidgetAsync(user, request.WidgetState, request.PublicName);
            return success ? Ok() : BadRequest();
        }

        // 3. Oblíbené
        [HttpGet("liked")]
        [Authorize]
        public async Task<IActionResult> GetLikedWidgets()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value
                     ?? User.FindFirst("email")?.Value;

            if (string.IsNullOrEmpty(email)) return Unauthorized();

            var widgets = await _couchService.GetLikedWidgetsAsync(email);
            return Ok(widgets);
        }

        // 4. Like
        [HttpPost("{id}/like")]
        [Authorize]
        public async Task<IActionResult> Like(string id)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value
                     ?? User.FindFirst("email")?.Value;

            if (string.IsNullOrEmpty(email)) return Unauthorized();

            await _couchService.ToggleLikeAsync(id, email);
            return Ok();
        }

        // 5. ZVLASTNĚNÍ – 100% funguje s tvým UserDoc + OpenWidgets
        [HttpPost("adopt")]
        [Authorize]
        public async Task<IActionResult> Adopt([FromBody] AdoptRequest request)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value
                     ?? User.FindFirst("email")?.Value;

            if (string.IsNullOrEmpty(email))
                return Unauthorized();

            var user = await _couchService.GetUserByEmailAsync(email);
            if (user == null)
                return NotFound("Uživatel nenalezen");

            // Nový widget – přesně podle tvé UserWidgetState
            var newWidget = new UserWidgetState
            {
                Name = request.WidgetType,
                Location = request.Settings?.TryGetValue("location", out var loc) == true ? loc : ""
            };

            // Přidáme do OpenWidgets
            user.OpenWidgets ??= new List<UserWidgetState>();
            user.OpenWidgets.Add(newWidget);

            // Uložíme pomocí stávající metody SaveUserWidgetsAsync (ta už vše umí správně)
            var success = await _couchService.SaveUserWidgetsAsync(email, user.OpenWidgets);

            return success
                ? Ok(new { message = "Widget byl úspěšně přidán do tvého dashboardu!" })
                : StatusCode(500, "Nepodařilo se uložit widget");
        }

        // DTOs
        public class PublishRequest
        {
            public string PublicName { get; set; } = string.Empty;
            public UserWidgetState WidgetState { get; set; } = null!;
        }

        public class AdoptRequest
        {
            public string WidgetType { get; set; } = string.Empty;
            public Dictionary<string, string>? Settings { get; set; }
        }
    }
}