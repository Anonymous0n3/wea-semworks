using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using WebApplication1.Service;
using WebApplication1.Models;

namespace WebApplication1.ViewComponents
{
    public class CountryInfoWidgetViewComponent : ViewComponent
    {
        private readonly CountryInfoService _service;
        private readonly IMemoryCache _cache;

        public CountryInfoWidgetViewComponent(CountryInfoService service, IMemoryCache cache)
        {
            _service = service;
            _cache = cache;
        }

        public async Task<IViewComponentResult> InvokeAsync(string isoCode = "CZ")
        {
            // Načti detail konkrétní země
            var country = await _service.GetCountryDetailsAsync(isoCode);

            var model = country; // přímo CountryInfoModel

            // AJAX = vracíme jen detail
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return View("_CountryResultPartial", model);

            // klasický render = celý widget
            return View("CountryInfoWidget", model);
        }
    }
}
