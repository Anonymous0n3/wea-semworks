using Microsoft.AspNetCore.Mvc;
using WebApplication1.Service;
using WebApplication1.Models;

namespace WebApplication1.ViewComponents
{
    public class CountryInfoWidgetViewComponent : ViewComponent
    {
        private readonly CountryInfoService _service;
        private readonly IDictionary<string, string> countries;
        public CountryInfoWidgetViewComponent(CountryInfoService service)
        {
            _service = service;
            countries = _service.GetAllCountriesAsync().Result;
        }

        /// <summary>
        /// Vrátí celý widget nebo jen fragment .country-result při AJAX requestu.
        /// </summary>
        /// <param name="isoCode">ISO kód země (default "CZ")</param>
        public async Task<IViewComponentResult> InvokeAsync(string isoCode = "CZ")
        {
            // načti detaily země
            var country = await _service.GetCountryDetailsAsync(isoCode);

            var modelCountry = new CountryInfoModel
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
                Countries = countries,
                SelectedCountry = modelCountry
            };

            // pokud jde o AJAX request, vrátíme jen .country-result partial
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return View("_CountryResultPartial", modelCountry);
            }

            // vrátíme celý widget
            return View("CountryInfoWidget", model);
        }
    }
}
