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

        // 1. Získání seznamu všech (filtr)
        [HttpPost("list")]
        [AllowAnonymous]
        public async Task<IActionResult> GetList([FromBody] WidgetFilterRequest filter)
        {
            var widgets = await _couchService.GetPublicWidgetsAsync(filter);
            return Ok(widgets);
        }

        // 2. Publikování widgetu
        [HttpPost("publish")]
        [Authorize]
        public async Task<IActionResult> Publish([FromBody] PublishRequest request)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var user = await _couchService.GetUserByEmailAsync(email);
            if (user == null) return Unauthorized();

            var success = await _couchService.PublishWidgetAsync(user, request.WidgetState, request.PublicName);
            if (!success) return BadRequest("Failed to publish");

            return Ok(new { message = "Published successfully" });
        }

        // 3. NOVÁ METODA: Získání oblíbených widgetů (GET /api/PublicWidgets/liked)
        [HttpGet("liked")]
        [Authorize]
        public async Task<IActionResult> GetLikedWidgets()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email)) return Unauthorized();

            // Tuto metodu musíme mít v Service (viz bod 2 níže)
            var widgets = await _couchService.GetLikedWidgetsAsync(email);
            return Ok(widgets);
        }

        // 4. OPRAVA: Akce Like/Unlike
        // V JS voláš ".../like" (bez D), ale v C# jsi měl "liked". Sjednotíme to na "like".
        [HttpPost("{id}/like")]
        [Authorize]
        public async Task<IActionResult> Like(string id)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            // Tady to volá ToggleLikeAsync
            var success = await _couchService.ToggleLikeAsync(id, email);

            if (!success) return BadRequest("Cannot like this widget");
            return Ok();
        }

        // DTO
        public class PublishRequest
        {
            public string PublicName { get; set; }
            public UserWidgetState WidgetState { get; set; }
        }
    }
}