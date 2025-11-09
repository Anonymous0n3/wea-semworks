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
            // Načti seznam států z cache
            if (!_cache.TryGetValue("AllCountries", out IDictionary<string, string> countries))
            {
                countries = await _service.GetAllCountriesAsync();
                _cache.Set("AllCountries", countries, TimeSpan.FromHours(12));
            }

            // Načti detail konkrétní země
            var country = await _service.GetCountryDetailsAsync(isoCode);

            // Pokud se nenašel, vytvoř fallback model
            var modelCountry = country ?? new CountryInfoModel
            {
                IsoCode = isoCode,
                Name = countries.ContainsKey(isoCode) ? countries[isoCode] : isoCode,
                CapitalCity = "-",
                Currency = "-",
                PhoneCode = "-",
                FlagUrl = ""
            };

            // Celý model pro widget
            var model = new CountryInfoViewModel
            {
                Countries = countries,
                SelectedCountry = modelCountry
            };

            // AJAX volání = jen partial
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return View("_CountryResultPartial", modelCountry);
            }

            // klasický render = celý widget
            return View("CountryInfoWidget", model);
        }
    }
}
