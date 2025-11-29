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

        [HttpPost("list")]
        [AllowAnonymous] // Anonymní uživatelé mohou prohlížet
        public async Task<IActionResult> GetList([FromBody] WidgetFilterRequest filter)
        {
            var widgets = await _couchService.GetPublicWidgetsAsync(filter);
            return Ok(widgets);
        }

        [HttpPost("publish")]
        [Authorize]
        public async Task<IActionResult> Publish([FromBody] PublishRequest request)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var user = await _couchService.GetUserByEmailAsync(email);
            if (user == null) return Unauthorized();

            var success = await _couchService.PublishWidgetAsync(user, request.WidgetState, request.PublicName);
            _logger
            if (!success) return BadRequest("Failed to publish");

            return Ok(new { message = "Published successfully" });
        }

        [HttpPost("{id}/like")]
        [Authorize]
        public async Task<IActionResult> Like(string id)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var success = await _couchService.ToggleLikeAsync(id, email);
            if (!success) return BadRequest("Cannot like this widget (maybe you are author or DB error)");
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