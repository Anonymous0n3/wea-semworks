using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Service;

namespace WebApplication1.Controllers
{
    public class CountryController : Controller
    {
        private readonly CountryInfoService _service;
        private readonly ILogger<CountryController> _logger;

        public CountryController(CountryInfoService service, ILogger<CountryController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Details(string isoCode = "CZ")
        {
            if (string.IsNullOrEmpty(isoCode))
                return BadRequest("Missing ISO code");

            // Načteme jen detail vybrané země
            var country = await _service.GetCountryDetailsAsync(isoCode);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                // AJAX = jen partial view s detailem země
                return PartialView("_CountryResultPartial", country);
            }

            // Normální render = ViewComponent, který načte jen vybranou zemi
            return ViewComponent("CountryInfoWidget", new { isoCode });
        }
    }
}
