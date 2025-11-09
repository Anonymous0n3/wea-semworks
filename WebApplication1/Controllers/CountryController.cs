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

            var country = await _service.GetCountryDetailsAsync(isoCode);
            var model = new CountryInfoModel
            {
                IsoCode = country.IsoCode,
                Name = country.Name,
                CapitalCity = country.CapitalCity,
                Currency = country.Currency,
                PhoneCode = country.PhoneCode,
                FlagUrl = country.FlagUrl
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CountryResultPartial", model);

            return ViewComponent("CountryInfoWidget", new { isoCode });
        }
    }
}
