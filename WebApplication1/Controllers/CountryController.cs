using Microsoft.AspNetCore.Mvc;

namespace WebApplication1.Controllers
{
    public class CountryController : Controller
    {
        [HttpGet]
        public IActionResult Details(string isoCode = "CZ")
        {
            // Controller už nevolá CountryInfoService ani nenaplňuje model
            // Jen předává parametry ViewComponentu
            return ViewComponent("CountryInfoWidget", new { isoCode });
        }
    }
}
