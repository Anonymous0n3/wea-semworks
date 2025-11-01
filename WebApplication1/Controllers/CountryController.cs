using Microsoft.AspNetCore.Mvc;
using WebApplication1.Service;
using WebApplication1.Models;

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

            var modelcountry = new CountryInfoModel
            {
                IsoCode = country.IsoCode,
                Name = country.Name,
                CapitalCity = country.CapitalCity,
                Currency = country.Currency,
                PhoneCode = country.PhoneCode,
                FlagUrl = country.FlagUrl
            };

            var model = new CountryInfoViewModel
            {
                Countries = await _service.GetAllCountriesAsync(),
                SelectedCountry = modelcountry
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                // vrátí jen HTML fragment pro AJAX
                return PartialView("_CountryResultPartial", modelcountry);
            }

            // vrátí celý widget
            return View("CountryInfoWidget", model);
        }
    }
}
